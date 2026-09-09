namespace Managementv2.Server

open Microsoft.Extensions.Logging
open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions

type FormFieldDefinition =
    { Name: string
      Title: string
      Type: string
      InputValidations: string[]
      InputHint: string }

type OperationSettings =
    { Title: string
      ExecutionForm: FormFieldDefinition[]
      MaxParallelism: int }

module OperationSettings =
    let private jsonOptions =
        JsonSerializerOptions(
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        )

    let empty =
        { Title = ""
          ExecutionForm = [||]
          MaxParallelism = 1 }

    let private withDefaults (settings: OperationSettings) =
        { Title = if isNull settings.Title then "" else settings.Title
          ExecutionForm =
            if isNull settings.ExecutionForm then
                [||]
            else
                settings.ExecutionForm
                |> Array.map (fun field ->
                    if isNull field.InputValidations then
                        { field with InputValidations = [||] }
                    else
                        field)
          MaxParallelism = max 1 settings.MaxParallelism }

    let ofJson (json: string) =
        let settings = JsonSerializer.Deserialize<OperationSettings>(json, jsonOptions)
        if isNull (box settings) then empty else withDefaults settings

    let toJson (settings: OperationSettings) =
        JsonSerializer.Serialize(withDefaults settings, jsonOptions)

type CustomOperation =
    { Name: string
      Settings: OperationSettings
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

    let calculatePath name =
        Path.Combine(baseDirectory, name, "calculate.ps1")

    let executePath name =
        Path.Combine(baseDirectory, name, "execute.ps1")

    let settingsPath name =
        Path.Combine(baseDirectory, name, "settings.json")

    let tryRead (name: string) : CustomOperation option =
        if not <| name.StartsWith "_" && File.Exists(settingsPath name) && File.Exists(executePath name) then
            try
                Some
                    { Name = name
                      Settings = OperationSettings.ofJson(File.ReadAllText(settingsPath name))
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

            File.WriteAllText(settingsPath operationName, OperationSettings.toJson operation.Settings)

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
