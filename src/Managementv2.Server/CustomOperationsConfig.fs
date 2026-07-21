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

/// JSON encoding of the config, shared by the file store and the HTTP API. Since
/// System.Text.Json can't serialize an F# union directly, each value is mapped to a
/// JsonNode by its kind:
///   text                  -> a JSON string
///   file                  -> { "file": "<base64>" }
///   credential            -> { "userName": "...", "password": "..." }
///   protected certificate -> { "file": "<base64>", "password": "..." }
///   ssh key               -> { "userName": "...", "keyFile": "<base64>" }
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

    let private valueOfJson (node: JsonNode) : ConfigValue =
        match node with
        | :? JsonObject as object when object.ContainsKey "userName" && object.ContainsKey "keyFile" ->
            SshKey(str object "userName", Convert.FromBase64String(str object "keyFile"))
        | :? JsonObject as object when object.ContainsKey "userName" ->
            Credential(str object "userName", str object "password")
        | :? JsonObject as object when object.ContainsKey "file" && object.ContainsKey "password" ->
            ProtectedCertificate(Convert.FromBase64String(str object "file"), str object "password")
        | :? JsonObject as object when object.ContainsKey "file" -> File(Convert.FromBase64String(str object "file"))
        | _ when node.GetValueKind() = JsonValueKind.String -> Text(node.GetValue<string>())
        | _ -> Text(node.ToJsonString())

    /// Serializes the config to a JSON object.
    let toJson (config: Map<string, ConfigValue>) : JsonObject =
        let object = JsonObject()

        for entry in config do
            object[entry.Key] <- valueToJson entry.Value

        object

    /// Parses a JSON object into a config.
    let ofJson (node: JsonNode) : Map<string, ConfigValue> =
        match node with
        | :? JsonObject as object ->
            object
            |> Seq.choose (fun entry ->
                match entry.Value with
                | null -> None
                | value -> Some(entry.Key, valueOfJson value))
            |> Map.ofSeq
        | _ -> Map.empty

/// Read/write access to the shared configuration (secrets) that custom operations
/// receive as their $Config parameter.
type ICustomOperationsConfig =
    abstract member Read: unit -> Map<string, ConfigValue>
    abstract member Write: config: Map<string, ConfigValue> -> unit

/// Stores the custom operations config as a JSON object in a file.
type JsonFileCustomOperationsConfig(filePath: string) =
    interface ICustomOperationsConfig with
        member _.Read() =
            if IO.File.Exists filePath then
                CustomOperationsConfig.ofJson (JsonNode.Parse(IO.File.ReadAllText filePath))
            else
                Map.empty

        member _.Write(config) =
            let directory = IO.Path.GetDirectoryName filePath

            if not (String.IsNullOrEmpty directory) then
                IO.Directory.CreateDirectory directory |> ignore

            let json = CustomOperationsConfig.toJson config
            IO.File.WriteAllText(filePath, json.ToJsonString(JsonSerializerOptions(WriteIndented = true)))