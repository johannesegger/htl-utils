namespace Managementv2.Server.Controllers

open Managementv2.Server
open Microsoft.AspNetCore.Mvc
open System.Text.Json.Nodes

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
    
    [<HttpGet>]
    member _.Get() =
        customOperationsStore.GetAll() |> List.map toDto

    [<HttpGet("calculated")>]
    member _.GetCalculatedOperations() =
        let config = customOperationsConfig.Read()

        let results =
            customOperationsStore.GetAll()
            |> Seq.choose (fun operation ->
                match operation.Calculate with
                | None -> None
                | Some calculate ->
                    match codeExecution.Execute config calculate with
                    | Ok data -> Some(Ok {| Name = operation.Name; Data = data |})
                    | Error e -> Some(Error e))

        ({| Operations = []; Errors = [] |}, results)
        ||> Seq.fold (fun state item ->
            match item with
            | Ok v ->
                {| state with
                    Operations = state.Operations @ [ v ] |}
            | Error error ->
                {| state with
                    Errors = state.Errors @ [ error ] |})

    [<HttpGet("{name}/calculated")>]
    member this.GetCalculatedOperation(name: string) =
        match customOperationsStore.TryGet name with
        | None -> this.NotFound() :> IActionResult
        | Some operation ->
            match operation.Calculate with
            | None -> this.NoContent() :> IActionResult
            | Some calculate ->
                let config = customOperationsConfig.Read()

                match codeExecution.Execute config calculate with
                | Ok data -> this.Ok data :> IActionResult
                | Error error -> this.StatusCode(500, error) :> IActionResult

    [<HttpGet("config")>]
    member _.GetConfig() : JsonNode =
        customOperationsConfig.Read() |> CustomOperationsConfig.toJson :> JsonNode

    [<HttpPut("config")>]
    member this.SetConfig([<FromBody>] config: JsonNode) =
        config |> CustomOperationsConfig.ofJson |> customOperationsConfig.Write
        this.NoContent() :> IActionResult

    [<HttpPost("execution")>]
    member this.Execute([<FromBody>] operation: {| Name: string; Data: JsonNode |}) =
        match customOperationsStore.TryGet operation.Name with
        | Some stored ->
            let config = customOperationsConfig.Read()

            match codeExecution.ExecuteWithInput config stored.Execute operation.Data with
            | Ok result -> this.Ok result :> IActionResult
            | Error error -> this.StatusCode(500, error) :> IActionResult
        | None -> this.NotFound()

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
