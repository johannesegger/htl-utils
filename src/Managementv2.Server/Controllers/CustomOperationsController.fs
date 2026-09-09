namespace Managementv2.Server.Controllers

open Managementv2.Server
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Mvc
open System.Text.Json
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks

[<ApiController>]
[<Route("api/custom-operations")>]
[<Authorize("ExecuteCustomOperations")>]
type CustomOperationsController
    (
        codeExecution: CodeExecution,
        customOperationsConfig: ICustomOperationsConfig,
        customOperationsStore: ICustomOperationsStore,
        executionGate: OperationExecutionGate
    ) =
    inherit ControllerBase()

    let toDto (operation: CustomOperation) =
        {| Id = operation.Id
           Settings = operation.Settings
           CanCalculate = Option.isSome operation.Calculate |}

    let toDefinitionDto (operation: CustomOperation) =
        {| Id = operation.Id
           Settings = operation.Settings
           Calculate = Option.toObj operation.Calculate
           Execute = operation.Execute |}

    let tryGetSettingsError (settings: OperationSettings) =
        if settings.MaxParallelism < 1 then
            Some $"maxParallelism must be at least 1, but was %d{settings.MaxParallelism}."
        else
            None

    let toOperation (id: string) (settings: OperationSettings) (calculate: string) (execute: string) : CustomOperation =
        { Id = id
          Settings = settings
          Calculate = Option.ofObj calculate
          Execute = execute }

    [<HttpGet>]
    member _.Get() =
        customOperationsStore.GetAll() |> List.map toDto

    [<HttpGet("definitions")>]
    [<Authorize("ManageCustomOperations")>]
    member _.GetDefinitions() =
        {|
            OperationDefinitions = customOperationsStore.GetAll() |> List.map toDefinitionDto
            Templates = {|
                Settings = OperationSettings.empty
                CalculateScript =
                    String.concat "\n" [
                        "param("
                        "    [Parameter(Mandatory = $true)] $Config"
                        ")"
                        ""
                    ]
                ExecuteScript =
                    String.concat "\n" [
                        "param("
                        "    [Parameter(Mandatory = $true)] $Config,"
                        "    [Parameter(Mandatory = $true)] $InputData"
                        ")"
                        ""
                    ]
            |}
        |}

    [<HttpGet("{id}/calculated")>]
    member this.GetCalculatedOperation(id: string, cancellationToken: CancellationToken) : Task<IActionResult> =
        task {
            match customOperationsStore.TryGet id with
            | None -> return this.NotFound() :> IActionResult
            | Some operation ->
                match operation.Calculate with
                | None -> return this.NoContent() :> IActionResult
                | Some calculate ->
                    let config = customOperationsConfig.Read()
                    let! result = codeExecution.Execute config calculate cancellationToken

                    match result with
                    | Ok (Some data) when data.GetValueKind() = JsonValueKind.Array -> return this.Ok data :> IActionResult
                    | Ok (Some data) -> return this.Ok [data] :> IActionResult
                    | Ok None -> return this.Ok [] :> IActionResult
                    | Error error -> return this.StatusCode(500, error) :> IActionResult
        }

    [<HttpGet("config")>]
    [<Authorize("ManageCustomOperations")>]
    member _.GetConfig() : JsonNode =
        customOperationsConfig.Read() |> CustomOperationsConfig.toJson :> JsonNode

    [<HttpPut("config")>]
    [<Authorize("ManageCustomOperations")>]
    member this.SetConfig([<FromBody>] config: JsonNode) =
        config |> CustomOperationsConfig.ofJson |> customOperationsConfig.Write
        this.NoContent() :> IActionResult

    [<HttpPost("execution")>]
    member this.Execute
        ([<FromBody>] operation: {| Id: string; Data: JsonNode |}, cancellationToken: CancellationToken) =
        task {
            match customOperationsStore.TryGet operation.Id with
            | Some stored ->
                let config = customOperationsConfig.Read()
                let run () = codeExecution.ExecuteWithInput config stored.Execute operation.Data cancellationToken

                let! result =
                    executionGate.Run(stored.Id, stored.Settings.MaxParallelism, run, cancellationToken)

                match result with
                | Ok data -> return this.Ok data :> IActionResult
                | Error error -> return this.StatusCode(500, error)
            | None -> return this.NotFound()
        }

    [<HttpPost>]
    [<Authorize("ManageCustomOperations")>]
    member this.Add
        ([<FromBody>] operation:
            {| Settings: OperationSettings
               Calculate: string
               Execute: string |})
        =
        match tryGetSettingsError operation.Settings with
        | Some error -> this.BadRequest error :> IActionResult
        | None ->

        let id = System.Guid.NewGuid().ToString()

        let created =
            toOperation id operation.Settings operation.Calculate operation.Execute
            |> customOperationsStore.Save

        this.Created($"custom-operations/%s{created.Id}", toDefinitionDto created) :> IActionResult

    [<HttpPut("{id}")>]
    [<Authorize("ManageCustomOperations")>]
    member this.Edit
        (
            id: string,
            [<FromBody>] operation:
                {| Settings: OperationSettings
                   Calculate: string
                   Execute: string |}
        ) =
        match tryGetSettingsError operation.Settings with
        | Some error -> this.BadRequest error :> IActionResult
        | None ->

        match customOperationsStore.TryGet id with
        | None -> this.NotFound() :> IActionResult
        | Some stored ->
            let updated =
                toOperation stored.Id operation.Settings operation.Calculate operation.Execute
                |> customOperationsStore.Save
            this.Ok(toDefinitionDto updated) :> IActionResult

    [<HttpDelete("{id}")>]
    [<Authorize("ManageCustomOperations")>]
    member this.Remove(id: string) =
        match customOperationsStore.TryGet id with
        | None -> this.NotFound() :> IActionResult
        | Some stored ->
            customOperationsStore.Remove stored.Id
            this.NoContent() :> IActionResult
