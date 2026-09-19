namespace Managementv2.Server

open System
open System.Text.Json
open System.Text.Json.Nodes

/// A custom operations config value.
type ConfigValue =
    /// Plain text.
    | Text of string
    /// The raw contents of a file.
    | File of byte[]
    /// A username/password pair.
    | Credential of userName: string * password: string
    /// A password-protected certificate: its file contents and the password.
    | ProtectedCertificate of certificate: byte[] * password: string
    /// An SSH login: a username and the contents of a private key file.
    | SshKey of userName: string * keyFile: byte[]

/// A config entry: a value plus a comment, blank when there is none. The comment is a note
/// for whoever edits the config; it is stored and shown in the UI but never reaches an operation.
type ConfigEntry =
    { Value: ConfigValue
      Comment: string }

module ConfigEntry =
    /// An entry with a blank comment.
    let ofValue value = { Value = value; Comment = "" }

/// JSON encoding of the config, shared by the file store and the HTTP API. Since
/// System.Text.Json can't serialize an F# union directly, each value is mapped to a
/// JsonNode by its kind:
///   text                  -> a JSON string
///   file                  -> { "file": "<base64>" }
///   credential            -> { "userName": "...", "password": "..." }
///   protected certificate -> { "file": "<base64>", "password": "..." }
///   ssh key               -> { "userName": "...", "keyFile": "<base64>" }
/// Every entry also carries a "comment" property, blank when there is none. Text has no
/// object form of its own, so it is written as { "text": "...", "comment": "..." }; a plain
/// JSON string is still read as text, with a blank comment.
module CustomOperationsConfig =
    let private str (object: JsonObject) (key: string) =
        match object[key] with
        | null -> ""
        | node -> node.GetValue<string>()

    let private valueToJson (value: ConfigValue) : JsonNode =
        match value with
        | Text text -> JsonValue.Create text
        | File bytes ->
            let object = JsonObject()
            object["file"] <- JsonValue.Create(Convert.ToBase64String bytes)
            object
        | Credential(userName, password) ->
            let object = JsonObject()
            object["userName"] <- JsonValue.Create userName
            object["password"] <- JsonValue.Create password
            object
        | ProtectedCertificate(certificate, password) ->
            let object = JsonObject()
            object["file"] <- JsonValue.Create(Convert.ToBase64String certificate)
            object["password"] <- JsonValue.Create password
            object
        | SshKey(userName, keyFile) ->
            let object = JsonObject()
            object["userName"] <- JsonValue.Create userName
            object["keyFile"] <- JsonValue.Create(Convert.ToBase64String keyFile)
            object

    let private entryToJson (entry: ConfigEntry) : JsonNode =
        let object =
            match valueToJson entry.Value with
            | :? JsonObject as object -> object
            | node ->
                // Text serializes to a bare string: give it an object form to hang the comment on.
                let object = JsonObject()
                object["text"] <- node
                object

        object["comment"] <- JsonValue.Create entry.Comment
        object

    let private valueOfJson (node: JsonNode) : ConfigValue =
        match node with
        | :? JsonObject as object when object.ContainsKey "userName" && object.ContainsKey "keyFile" ->
            SshKey(str object "userName", Convert.FromBase64String(str object "keyFile"))
        | :? JsonObject as object when object.ContainsKey "userName" ->
            Credential(str object "userName", str object "password")
        | :? JsonObject as object when object.ContainsKey "file" && object.ContainsKey "password" ->
            ProtectedCertificate(Convert.FromBase64String(str object "file"), str object "password")
        | :? JsonObject as object when object.ContainsKey "file" -> File(Convert.FromBase64String(str object "file"))
        | :? JsonObject as object when object.ContainsKey "text" -> Text(str object "text")
        | _ when node.GetValueKind() = JsonValueKind.String -> Text(node.GetValue<string>())
        | _ -> Text(node.ToJsonString())

    let private entryOfJson (node: JsonNode) : ConfigEntry =
        { Value = valueOfJson node
          Comment =
            match node with
            | :? JsonObject as object -> str object "comment"
            | _ -> "" }

    /// Serializes the config to a JSON object.
    let toJson (config: Map<string, ConfigEntry>) : JsonObject =
        let object = JsonObject()

        for entry in config do
            object[entry.Key] <- entryToJson entry.Value

        object

    /// Parses a JSON object into a config.
    let ofJson (node: JsonNode) : Map<string, ConfigEntry> =
        match node with
        | :? JsonObject as object ->
            object
            |> Seq.choose (fun entry ->
                match entry.Value with
                | null -> None
                | value -> Some(entry.Key, entryOfJson value))
            |> Map.ofSeq
        | _ -> Map.empty

/// Read/write access to the shared configuration (secrets) that custom operations
/// receive as their $Config parameter.
type ICustomOperationsConfig =
    abstract member Read: unit -> Map<string, ConfigEntry>
    abstract member Write: config: Map<string, ConfigEntry> -> unit

/// Stores the custom operations config as a JSON object in a file.
type JsonFileCustomOperationsConfig(filePath: string) =
    interface ICustomOperationsConfig with
        member _.Read() =
            if IO.File.Exists filePath then
                CustomOperationsConfig.ofJson (JsonNode.Parse(IO.File.ReadAllText filePath))
            else
                Map.empty

        member _.Write config =
            let directory = IO.Path.GetDirectoryName filePath

            if not (String.IsNullOrEmpty directory) then
                IO.Directory.CreateDirectory directory |> ignore

            let json = CustomOperationsConfig.toJson config
            IO.File.WriteAllText(filePath, json.ToJsonString(JsonSerializerOptions(WriteIndented = true)))