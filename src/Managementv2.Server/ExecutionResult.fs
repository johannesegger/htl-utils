namespace Managementv2.Server

open Microsoft.AspNetCore.StaticFiles
open System
open System.Management.Automation
open System.Text.Json.Nodes

/// A file produced by an operation script, to be offered to the user as a download.
/// Scripts create one with New-FileResult; the server recognizes it by type among the
/// script's output objects.
type ResultFile =
    { Name: string
      ContentType: string
      Content: byte[] }

/// What an operation script produced. Qualified access because `File` and `Text` would
/// otherwise collide with the ConfigValue cases of the same name.
[<RequireQualifiedAccess>]
type ExecutionResult =
    /// The script wrote nothing.
    | Empty
    /// A single string, shown as-is.
    | Text of string
    /// Anything else, converted to JSON by the script's runspace.
    | Json of JsonNode
    /// A file to download.
    | File of ResultFile

/// JSON encoding of an execution result, consumed by the client (see its ExecutionOutput).
/// Every result is an object tagged with its kind:
///   empty -> { "kind": "empty" }
///   text  -> { "kind": "text", "text": "..." }
///   json  -> { "kind": "json", "data": <anything the script returned> }
///   file  -> { "kind": "file", "name": "...", "contentType": "...", "content": "<base64>" }
[<RequireQualifiedAccess>]
module ExecutionResult =
    let toJson (result: ExecutionResult) : JsonNode =
        let object = JsonObject()

        match result with
        | ExecutionResult.Empty -> object["kind"] <- JsonValue.Create "empty"
        | ExecutionResult.Text text ->
            object["kind"] <- JsonValue.Create "text"
            object["text"] <- JsonValue.Create text
        | ExecutionResult.Json data ->
            object["kind"] <- JsonValue.Create "json"
            object["data"] <- data
        | ExecutionResult.File file ->
            object["kind"] <- JsonValue.Create "file"
            object["name"] <- JsonValue.Create file.Name
            object["contentType"] <- JsonValue.Create file.ContentType
            object["content"] <- JsonValue.Create(Convert.ToBase64String file.Content)

        object

[<RequireQualifiedAccess>]
module ContentType =
    let private provider = FileExtensionContentTypeProvider()

    /// The content type for a file name, guessed from its extension.
    let ofFileName (fileName: string) =
        match provider.TryGetContentType fileName with
        | true, contentType -> contentType
        | _ -> Net.Mime.MediaTypeNames.Application.Octet

/// New-FileResult: makes the operation's result a file for the user to download.
///   New-FileResult -Name 'report.pdf' -Content $bytes [-ContentType 'application/pdf']
///   New-FileResult -Path $path [-Name 'report.pdf'] [-ContentType ...]
/// Nothing is taken from the pipeline: the pipeline unrolls a byte array into single
/// bytes, so the contents are always passed as a parameter.
/// Only the result of the whole script counts: a script that writes anything besides the
/// one file result has its output converted to JSON like any other script.
[<Cmdlet(VerbsCommon.New, "FileResult", DefaultParameterSetName = "Content")>]
[<OutputType(typeof<ResultFile>)>]
type NewFileResultCommand() =
    inherit PSCmdlet()

    /// The file contents.
    [<Parameter(Mandatory = true, ParameterSetName = "Content", Position = 0)>]
    member val Content: byte[] = null with get, set

    /// A file to read the contents from. Read right away, so the script is free to delete it.
    [<Parameter(Mandatory = true, ParameterSetName = "Path", Position = 0)>]
    member val Path = "" with get, set

    /// The name the file is downloaded under. Defaults to the name of -Path.
    [<Parameter(Mandatory = true, ParameterSetName = "Content")>]
    [<Parameter(ParameterSetName = "Path")>]
    member val Name = "" with get, set

    /// Defaults to the type registered for the extension of the file name, or to
    /// application/octet-stream when there is none.
    [<Parameter>]
    member val ContentType = "" with get, set

    override this.EndProcessing() =
        let name, content =
            match this.ParameterSetName with
            | "Path" ->
                let path = this.GetUnresolvedProviderPathFromPSPath this.Path

                let name =
                    if String.IsNullOrEmpty this.Name then
                        IO.Path.GetFileName path
                    else
                        this.Name

                name, IO.File.ReadAllBytes path
            | _ -> this.Name, this.Content

        this.WriteObject
            { Name = name
              ContentType =
                if String.IsNullOrEmpty this.ContentType then
                    ContentType.ofFileName name
                else
                    this.ContentType
              Content = content }
