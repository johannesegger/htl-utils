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

let private form (json: string) = JsonNode.Parse json

let tests =
    testList
        "FileSystemCustomOperationsStore"
        [ testCase "Save then TryGet round-trips an operation with a calculate script"
          <| fun () ->
              withStore (fun store ->
                  let op =
                      store.Save {
                        Name = "create-teacher"
                        Form = form """{"fields":["a"]}"""
                        Calculate = Some "calc"
                        Execute = "exec" }

                  match store.TryGet "create-teacher" with
                  | Some read ->
                      Expect.equal read.Name op.Name "name"
                      Expect.equal read.Calculate op.Calculate "calculate"
                      Expect.equal read.Execute op.Execute "execute"
                      Expect.equal (read.Form.ToJsonString()) (op.Form.ToJsonString()) "form"
                  | None -> failtest "Expected the operation to be found")

          testCase "Save without a calculate script leaves Calculate = None"
          <| fun () ->
              withStore (fun store ->
                  store.Save
                      { Name = "op"
                        Form = form "{}"
                        Calculate = None
                        Execute = "exec" } |> ignore

                  Expect.equal (store.TryGet "op" |> Option.get).Calculate None "No calculate script")

          testCase "Saving over an operation can remove its calculate script"
          <| fun () ->
              withStore (fun store ->
                  store.Save
                      { Name = "op"
                        Form = form "{}"
                        Calculate = Some "calc"
                        Execute = "e" } |> ignore

                  store.Save
                      { Name = "op"
                        Form = form "{}"
                        Calculate = None
                        Execute = "e" } |> ignore

                  Expect.equal (store.TryGet "op" |> Option.get).Calculate None "Calculate should be gone")

          testCase "GetAll returns saved operations"
          <| fun () ->
              withStore (fun store ->
                  store.Save
                      { Name = "a"
                        Form = form "{}"
                        Calculate = None
                        Execute = "e" } |> ignore

                  store.Save
                      { Name = "b"
                        Form = form "{}"
                        Calculate = None
                        Execute = "e" } |> ignore

                  let names = store.GetAll() |> List.map (fun o -> o.Name) |> List.sort
                  Expect.equal names [ "a"; "b" ] "Both operations")

          testCase "Remove deletes an operation"
          <| fun () ->
              withStore (fun store ->
                  store.Save
                      { Name = "op"
                        Form = form "{}"
                        Calculate = None
                        Execute = "e" } |> ignore

                  store.Remove "op"
                  Expect.isNone (store.TryGet "op") "Should be gone")

          testCase "Save rejects an unsafe name"
          <| fun () ->
              withStore (fun store ->
                  Expect.throwsT<System.ArgumentException>
                      (fun () ->
                          store.Save
                              { Name = "../evil"
                                Form = form "{}"
                                Calculate = None
                                Execute = "e" } |> ignore)
                      "Should reject path traversal")

          testCase "TryGet with an unsafe name returns None"
          <| fun () -> withStore (fun store -> Expect.isNone (store.TryGet "../evil") "Unsafe name -> None") ]