namespace Managementv2.Server.Controllers

open Managementv2.Server
open Microsoft.AspNetCore.Mvc
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open System.Text.Json

[<ApiController>]
[<Route("api/custom-operations")>]
type CustomOperationsController
    (
        codeExecution: CodeExecution,
        customOperationsConfig: ICustomOperationsConfig,
        customOperationsStore: ICustomOperationsStore
    ) =
    inherit ControllerBase()

    let toDto (operation: CustomOperation) =
        {| Name = operation.Name
           Form = operation.Form
           Calculate = Option.toObj operation.Calculate
           Execute = operation.Execute |}

    let toOperation (name: string) (form: JsonNode) (calculate: string) (execute: string) : CustomOperation =
        { Name = name
          Form = form
          Calculate = Option.ofObj calculate
          Execute = execute }
    
    [<HttpGet()>]
    member _.Get() =
        customOperationsStore.GetAll() |> List.map toDto

    [<HttpGet("{name}/calculated")>]
    member this.GetCalculatedOperation(name: string, cancellationToken: CancellationToken) : Task<IActionResult> =
        task {
            match customOperationsStore.TryGet name with
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
    member _.GetConfig() : JsonNode =
        customOperationsConfig.Read() |> CustomOperationsConfig.toJson :> JsonNode

    [<HttpPut("config")>]
    member this.SetConfig([<FromBody>] config: JsonNode) =
        config |> CustomOperationsConfig.ofJson |> customOperationsConfig.Write
        this.NoContent() :> IActionResult

    [<HttpPost("execution")>]
    member this.Execute
        ([<FromBody>] operation: {| Name: string; Data: JsonNode |}, cancellationToken: CancellationToken) =
        task {
            match customOperationsStore.TryGet operation.Name with
            | Some stored ->
                let config = customOperationsConfig.Read()
                let! result = codeExecution.ExecuteWithInput config stored.Execute operation.Data cancellationToken

                match result with
                | Ok data -> return this.Ok data :> IActionResult
                | Error error -> return this.StatusCode(500, error)
            | None -> return this.NotFound()
        }

    [<HttpPost>]
    member this.Add
        ([<FromBody>] operation:
            {| Name: string
               Form: JsonNode
               Calculate: string
               Execute: string |})
        =
        match customOperationsStore.TryGet operation.Name with
        | Some _ -> this.Conflict($"A custom operation named '%s{operation.Name}' already exists.") :> IActionResult
        | None ->
            try
                let created =
                    toOperation operation.Name operation.Form operation.Calculate operation.Execute
                    |> customOperationsStore.Save
                this.Created($"custom-operations/%s{created.Name}", toDto created) :> IActionResult
            with :? System.ArgumentException as e ->
                this.BadRequest(e.Message) :> IActionResult

    [<HttpPut("{name}")>]
    member this.Edit
        (
            name: string,
            [<FromBody>] operation:
                {| Form: JsonNode
                   Calculate: string
                   Execute: string |}
        ) =
        match customOperationsStore.TryGet name with
        | None -> this.NotFound() :> IActionResult
        | Some _ ->
            let updated =
                toOperation name operation.Form operation.Calculate operation.Execute
                |> customOperationsStore.Save
            this.Ok(toDto updated) :> IActionResult

    [<HttpDelete("{name}")>]
    member this.Remove(name: string) =
        match customOperationsStore.TryGet name with
        | None -> this.NotFound() :> IActionResult
        | Some _ ->
            customOperationsStore.Remove name
            this.NoContent() :> IActionResult
