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
                        Id = "create-teacher"
                        Settings =
                          settings """{"title":"Create teacher","executionForm":[{"name":"userName","title":"User name","type":"text","inputValidations":["notEmpty"],"inputHint":"e.g. eina"}],"maxParallelism":5}"""
                        Calculate = Some "calc"
                        Execute = "exec" }

                  match store.TryGet "create-teacher" with
                  | Some read ->
                      Expect.equal read.Id op.Id "id"
                      Expect.equal read.Calculate op.Calculate "calculate"
                      Expect.equal read.Execute op.Execute "execute"
                      Expect.equal read.Settings op.Settings "settings"
                  | None -> failtest "Expected the operation to be found")

          testCase "Save without a calculate script leaves Calculate = None"
          <| fun () ->
              withStore (fun store ->
                  store.Save
                      { Id = "op"
                        Settings = settings "{}"
                        Calculate = None
                        Execute = "exec" } |> ignore

                  Expect.equal (store.TryGet "op" |> Option.get).Calculate None "No calculate script")

          testCase "Saving over an operation can remove its calculate script"
          <| fun () ->
              withStore (fun store ->
                  store.Save
                      { Id = "op"
                        Settings = settings "{}"
                        Calculate = Some "calc"
                        Execute = "e" } |> ignore

                  store.Save
                      { Id = "op"
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
                      { Id = "a"
                        Settings = settings "{}"
                        Calculate = None
                        Execute = "e" } |> ignore

                  store.Save
                      { Id = "b"
                        Settings = settings "{}"
                        Calculate = None
                        Execute = "e" } |> ignore

                  let ids = store.GetAll() |> List.map _.Id |> List.sort
                  Expect.equal ids [ "a"; "b" ] "Both operations")

          testCase "GetAll sorts operations by title"
          <| fun () ->
              withStore (fun store ->
                  for id, title in [ "op-c", "Zeugnis drucken"; "op-a", "abschluss"; "op-b", "Lehrer anlegen" ] do
                      store.Save
                          { Id = id
                            Settings = { OperationSettings.empty with Title = title }
                            Calculate = None
                            Execute = "e" } |> ignore

                  let titles = store.GetAll() |> List.map _.Settings.Title
                  Expect.equal titles [ "abschluss"; "Lehrer anlegen"; "Zeugnis drucken" ] "Sorted by title, ignoring case")

          testCase "Remove deletes an operation"
          <| fun () ->
              withStore (fun store ->
                  store.Save
                      { Id = "op"
                        Settings = settings "{}"
                        Calculate = None
                        Execute = "e" } |> ignore

                  store.Remove "op"
                  Expect.isNone (store.TryGet "op") "Should be gone")

          testCase "Save cleans an unsafe id"
          <| fun () ->
              withStore (fun store ->
                  let saved =
                      store.Save
                          { Id = "../evil"
                            Settings = settings "{}"
                            Calculate = None
                            Execute = "e" }

                  Expect.equal saved.Id "evil" "Unsafe characters are stripped from the id"
                  Expect.isSome (store.TryGet "evil") "Operation is saved under the cleaned id")

          testCase "TryGet with an unsafe id returns None"
          <| fun () -> withStore (fun store -> Expect.isNone (store.TryGet "../evil") "Unsafe id -> None") ]
