module Managementv2.Server.Test.CustomOperationsStore

open Expecto
open System.IO
open System.Text.Json
open Managementv2.Server
open Microsoft.Extensions.Logging.Abstractions

let private withStore (test: ICustomOperationsStore -> unit) =
    let baseDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
    Directory.CreateDirectory baseDir |> ignore

    let logger = NullLogger<FileSystemCustomOperationsStore>.Instance
    try
        test (FileSystemCustomOperationsStore(baseDir, logger) :> ICustomOperationsStore)
    finally
        Directory.Delete(baseDir, true)

let private settings (json: string) = OperationSettings.ofJson json

let tests =
    testList
        "FileSystemCustomOperationsStore"
        [ testCase "Save then TryGet round-trips an operation with a calculate script"
          <| fun () ->
              withStore (fun store ->
                  let op =
                      store.Save {
                        Name = "create-teacher"
                        Settings =
                          settings """{"title":"Create teacher","executionForm":[{"name":"userName","title":"User name","type":"text","inputValidations":["notEmpty"],"inputHint":"e.g. eina"}],"maxParallelism":5}"""
                        Calculate = Some "calc"
                        Execute = "exec" }

                  match store.TryGet "create-teacher" with
                  | Some read ->
                      Expect.equal read.Name op.Name "name"
                      Expect.equal read.Calculate op.Calculate "calculate"
                      Expect.equal read.Execute op.Execute "execute"
                      Expect.equal read.Settings op.Settings "settings"
                  | None -> failtest "Expected the operation to be found")

          testCase "Save without a calculate script leaves Calculate = None"
          <| fun () ->
              withStore (fun store ->
                  store.Save
                      { Name = "op"
                        Settings = settings "{}"
                        Calculate = None
                        Execute = "exec" } |> ignore

                  Expect.equal (store.TryGet "op" |> Option.get).Calculate None "No calculate script")

          testCase "Saving over an operation can remove its calculate script"
          <| fun () ->
              withStore (fun store ->
                  store.Save
                      { Name = "op"
                        Settings = settings "{}"
                        Calculate = Some "calc"
                        Execute = "e" } |> ignore

                  store.Save
                      { Name = "op"
                        Settings = settings "{}"
                        Calculate = None
                        Execute = "e" } |> ignore

                  Expect.equal (store.TryGet "op" |> Option.get).Calculate None "Calculate should be gone")

          testCase "Settings that are missing fields are read with their defaults"
          <| fun () ->
              Expect.equal (settings "{}") OperationSettings.empty "Empty settings"

          testTheory "Max parallelism is at least 1" [
              """{"title":"x","executionForm":[]}""" // maxParallelism is missing
              """{"maxParallelism":0}"""             // maxParallelism is below the minimum
              """{"maxParallelism":-3}"""            // maxParallelism is below the minimum
          ]
          <| fun json ->
              Expect.equal (settings json).MaxParallelism 1 "Max parallelism"

          testCase "Settings that don't match the schema can't be read"
          <| fun () ->
              Expect.throws (fun () -> settings """{"maxParallelism":"lots"}""" |> ignore) "Invalid settings"

          testCase "Settings are bound from a request body"
          <| fun () ->
              let json =
                  """{"title":"Create teacher","executionForm":[{"name":"userName","title":"User name","type":"text","inputValidations":["notEmpty"],"inputHint":null}],"maxParallelism":3}"""

              let bound = JsonSerializer.Deserialize<OperationSettings>(json, JsonSerializerOptions JsonSerializerDefaults.Web)

              Expect.equal bound (settings json) "Bound settings match the stored ones"
              Expect.equal bound.MaxParallelism 3 "Max parallelism"
              Expect.equal bound.ExecutionForm[0].Name "userName" "Form field name"

          testCase "GetAll returns saved operations"
          <| fun () ->
              withStore (fun store ->
                  store.Save
                      { Name = "a"
                        Settings = settings "{}"
                        Calculate = None
                        Execute = "e" } |> ignore

                  store.Save
                      { Name = "b"
                        Settings = settings "{}"
                        Calculate = None
                        Execute = "e" } |> ignore

                  let names = store.GetAll() |> List.map (fun o -> o.Name) |> List.sort
                  Expect.equal names [ "a"; "b" ] "Both operations")

          testCase "Remove deletes an operation"
          <| fun () ->
              withStore (fun store ->
                  store.Save
                      { Name = "op"
                        Settings = settings "{}"
                        Calculate = None
                        Execute = "e" } |> ignore

                  store.Remove "op"
                  Expect.isNone (store.TryGet "op") "Should be gone")

          testCase "Save cleans an unsafe name"
          <| fun () ->
              withStore (fun store ->
                  let saved =
                      store.Save
                          { Name = "../evil"
                            Settings = settings "{}"
                            Calculate = None
                            Execute = "e" }

                  Expect.equal saved.Name "evil" "Unsafe characters are stripped from the name"
                  Expect.isSome (store.TryGet "evil") "Operation is saved under the cleaned name")

          testCase "TryGet with an unsafe name returns None"
          <| fun () -> withStore (fun store -> Expect.isNone (store.TryGet "../evil") "Unsafe name -> None") ]
