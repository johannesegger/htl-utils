namespace Managementv2.Server

open Microsoft.Extensions.Logging
open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.RegularExpressions

type CustomOperation =
    { Name: string
      Form: JsonNode
      Calculate: string option
      Execute: string }

type ICustomOperationsStore =
    abstract member GetAll: unit -> CustomOperation list
    abstract member TryGet: name: string -> CustomOperation option
    abstract member Save: operation: CustomOperation -> CustomOperation
    abstract member Remove: name: string -> unit

type FileSystemCustomOperationsStore(baseDirectory: string, logger: ILogger<FileSystemCustomOperationsStore>) =
    let cleanName name =
        Regex.Replace(name, "[^a-zA-Z0-9-_]", "")

    let formPath name =
        Path.Combine(baseDirectory, name, "form.json")

    let calculatePath name =
        Path.Combine(baseDirectory, name, "calculate.ps1")

    let executePath name =
        Path.Combine(baseDirectory, name, "execute.ps1")

    let tryRead (name: string) : CustomOperation option =
        if not <| name.StartsWith "_" && File.Exists(formPath name) && File.Exists(executePath name) then
            try
                Some
                    { Name = name
                      Form = JsonNode.Parse(File.ReadAllText(formPath name))
                      Calculate =
                        if File.Exists(calculatePath name) then
                            Some(File.ReadAllText(calculatePath name))
                        else
                            None
                      Execute = File.ReadAllText(executePath name) }
            with e ->
                logger.LogWarning(e, "Error while reading custom operation {CustomOperationName}", name)
                None
        else
            logger.LogInformation("Skipping custom operation {CustomOperationName}", name)
            None

    interface ICustomOperationsStore with
        member _.GetAll() =
            if Directory.Exists baseDirectory then
                Directory.GetDirectories baseDirectory
                |> Seq.map Path.GetFileName
                |> Seq.choose tryRead
                |> Seq.sortBy _.Name
                |> List.ofSeq
            else
                []

        member _.TryGet name = cleanName name |> tryRead

        member _.Save operation =
            let operationName = cleanName operation.Name
            if String.IsNullOrEmpty operationName then
                invalidArg (nameof operation) $"Invalid custom operation name '%s{operation.Name}'."

            Directory.CreateDirectory(Path.Combine(baseDirectory, operationName)) |> ignore

            File.WriteAllText(
                formPath operationName,
                operation.Form.ToJsonString(JsonSerializerOptions(WriteIndented = true))
            )

            File.WriteAllText(
                executePath operationName,
                operation.Execute)

            match operation.Calculate with
            | Some calculate when not <| String.IsNullOrWhiteSpace calculate ->
                File.WriteAllText(calculatePath operationName, calculate)
            | _ ->
                if File.Exists(calculatePath operationName) then
                    File.Delete(calculatePath operationName)

            { operation with Name = operationName }

        member _.Remove name =
            let operationName = cleanName name
            if String.IsNullOrEmpty operationName then
                invalidArg (nameof name) $"Invalid custom operation name '%s{name}'."

            let directory = Path.Combine(baseDirectory, operationName)

            if Directory.Exists directory then
                Directory.Delete(directory, recursive = true)