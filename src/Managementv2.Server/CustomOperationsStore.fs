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
    { Id: string
      Settings: OperationSettings
      Calculate: string option
      Execute: string }

type ICustomOperationsStore =
    abstract member GetAll: unit -> CustomOperation list
    abstract member TryGet: id: string -> CustomOperation option
    abstract member Save: operation: CustomOperation -> CustomOperation
    abstract member Remove: id: string -> unit

type FileSystemCustomOperationsStore(baseDirectory: string, logger: ILogger<FileSystemCustomOperationsStore>) =
    let cleanId id =
        Regex.Replace(id, "[^a-zA-Z0-9-_]", "")

    let calculatePath id =
        Path.Combine(baseDirectory, id, "calculate.ps1")

    let executePath id =
        Path.Combine(baseDirectory, id, "execute.ps1")

    let settingsPath id =
        Path.Combine(baseDirectory, id, "settings.json")

    let tryRead (id: string) : CustomOperation option =
        if not <| id.StartsWith "_" && File.Exists(settingsPath id) && File.Exists(executePath id) then
            try
                Some
                    { Id = id
                      Settings = OperationSettings.ofJson(File.ReadAllText(settingsPath id))
                      Calculate =
                        if File.Exists(calculatePath id) then
                            Some(File.ReadAllText(calculatePath id))
                        else
                            None
                      Execute = File.ReadAllText(executePath id) }
            with e ->
                logger.LogWarning(e, "Error while reading custom operation {CustomOperationId}", id)
                None
        else
            logger.LogInformation("Skipping custom operation {CustomOperationId}", id)
            None

    interface ICustomOperationsStore with
        member _.GetAll() =
            if Directory.Exists baseDirectory then
                Directory.GetDirectories baseDirectory
                |> Seq.map Path.GetFileName
                |> Seq.choose tryRead
                |> Seq.sortWith (fun a b ->
                    String.Compare(a.Settings.Title, b.Settings.Title, StringComparison.InvariantCultureIgnoreCase))
                |> List.ofSeq
            else
                []

        member _.TryGet id = cleanId id |> tryRead

        member _.Save operation =
            let operationId = cleanId operation.Id
            if String.IsNullOrEmpty operationId then
                invalidArg (nameof operation) $"Invalid custom operation id '%s{operation.Id}'."

            Directory.CreateDirectory(Path.Combine(baseDirectory, operationId)) |> ignore

            File.WriteAllText(settingsPath operationId, OperationSettings.toJson operation.Settings)

            File.WriteAllText(
                executePath operationId,
                operation.Execute)

            match operation.Calculate with
            | Some calculate when not <| String.IsNullOrWhiteSpace calculate ->
                File.WriteAllText(calculatePath operationId, calculate)
            | _ ->
                if File.Exists(calculatePath operationId) then
                    File.Delete(calculatePath operationId)

            { operation with Id = operationId }

        member _.Remove id =
            let operationId = cleanId id
            if String.IsNullOrEmpty operationId then
                invalidArg (nameof id) $"Invalid custom operation id '%s{id}'."

            let directory = Path.Combine(baseDirectory, operationId)

            if Directory.Exists directory then
                Directory.Delete(directory, recursive = true)
