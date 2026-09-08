module Managementv2.Server.Test.CustomOperationsStore

open Expecto
open System.IO
open System.Text.Json.Nodes
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

let private settings (json: string) = JsonNode.Parse json

let tests =
    testList
        "FileSystemCustomOperationsStore"
        [ testCase "Save then TryGet round-trips an operation with a calculate script"
          <| fun () ->
              withStore (fun store ->
                  let op =
                      store.Save {
                        Name = "create-teacher"
                        Settings = settings """{"title":"Create teacher","executionForm":["a"],"executionMode":"parallel"}"""
                        Calculate = Some "calc"
                        Execute = "exec" }

                  match store.TryGet "create-teacher" with
                  | Some read ->
                      Expect.equal read.Name op.Name "name"
                      Expect.equal read.Calculate op.Calculate "calculate"
                      Expect.equal read.Execute op.Execute "execute"
                      Expect.equal (read.Settings.ToJsonString()) (op.Settings.ToJsonString()) "settings"
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

          testCase "Save then TryGet round-trips the max parallelism in the settings"
          <| fun () ->
              withStore (fun store ->
                  store.Save
                      { Name = "op"
                        Settings = settings """{"maxParallelism":5}"""
                        Calculate = None
                        Execute = "e" } |> ignore

                  let read = store.TryGet "op" |> Option.get
                  Expect.equal (MaxParallelism.ofSettings read.Settings) 5 "Max parallelism is persisted")

          testTheory "MaxParallelism defaults to 1" [
              """{"title":"x","executionForm":[]}""" // maxParallelism is missing
              "[]"                                   // the settings is not an object
              """{"maxParallelism":"nonsense"}"""    // maxParallelism is not a number
              """{"maxParallelism":1.5}"""           // maxParallelism is not an integer
              """{"maxParallelism":0}"""             // maxParallelism is below the minimum
              """{"maxParallelism":-3}"""            // maxParallelism is below the minimum
          ]
          <| fun json ->
              Expect.equal (MaxParallelism.ofSettings (settings json)) 1 "defaults to 1"

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
