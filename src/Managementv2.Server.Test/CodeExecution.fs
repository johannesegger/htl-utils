module Managementv2.Server.Test.CodeExecution

open Managementv2.Server
open System.IO
open System.Text
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open Expecto

let private await (task: Task<_>) = task |> Async.AwaitTask |> Async.RunSynchronously

let private toJsonString (node: JsonNode) = node.ToJsonString()

/// Runs a script as an operation, with the param block every execute script has.
let private run code =
    let script = $"param($Config, $InputData)\n%s{code}"

    CodeExecution().ExecuteOperation Map.empty script (JsonNode.Parse "{}") CancellationToken.None
    |> await

/// Runs a script as a calculation.
let private calculate code =
    CodeExecution().ExecuteCalculation Map.empty $"param($Config)\n%s{code}" CancellationToken.None
    |> await

let private expectOk result =
    match result with
    | Ok value -> value
    | Error error -> failtestf "Expected a result, but the script failed with: %s" error

let tests =
    testList
        "CodeExecution"
        [ testCase "A script without output produces no result"
          <| fun () -> Expect.equal (run "" |> expectOk) ExecutionResult.Empty "Should be empty"

          testCase "A lone string produces a text result"
          <| fun () ->
              Expect.equal
                  (run "'User EGGJ created'" |> expectOk)
                  (ExecutionResult.Text "User EGGJ created")
                  "Should be the string itself, not JSON"

          testCase "An object produces a json result"
          <| fun () ->
              match run "[pscustomobject]@{ Name = 'EGGJ' }" |> expectOk with
              | ExecutionResult.Json data -> Expect.equal (data.ToJsonString()) """{"Name":"EGGJ"}""" "Should be the object"
              | result -> failtestf "Expected a json result, but got %A" result

          testCase "Several strings produce a json array"
          <| fun () ->
              match run "'one'; 'two'" |> expectOk with
              | ExecutionResult.Json data -> Expect.equal (data.ToJsonString()) """["one","two"]""" "Should be an array"
              | result -> failtestf "Expected a json result, but got %A" result

          testCase "New-FileResult -Content produces a file result"
          <| fun () ->
              Expect.equal
                  (run "New-FileResult -Name 'report.pdf' -Content ([Text.Encoding]::UTF8.GetBytes('hello'))"
                   |> expectOk)
                  (ExecutionResult.File
                      { Name = "report.pdf"
                        ContentType = "application/pdf"
                        Content = Encoding.UTF8.GetBytes "hello" })
                  "Should carry the name, the guessed content type and the bytes"

          testCase "New-FileResult -Path reads the file and names the result after it"
          <| fun () ->
              let path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt")

              try
                  File.WriteAllText(path, "hello")

                  Expect.equal
                      (run $"New-FileResult -Path '%s{path}'" |> expectOk)
                      (ExecutionResult.File
                          { Name = Path.GetFileName path
                            ContentType = "text/plain"
                            Content = Encoding.UTF8.GetBytes "hello" })
                      "Should read the contents and default the name to the file name"
              finally
                  File.Delete path

          testCase "An error in the script is reported"
          <| fun () ->
              match run "Write-Error 'Boom'" with
              | Ok result -> failtestf "Expected an error, but got %A" result
              | Error error -> Expect.stringContains error "Boom" "Should carry the error message"

          testCase "ConvertTo-Pdf renders a PDF"
          <| fun () ->
              match run "New-FileResult -Name 'report.pdf' -Content (ConvertTo-Pdf -Html '<h1>Hello</h1>')"
                    |> expectOk with
              | ExecutionResult.File file ->
                  Expect.equal file.ContentType "application/pdf" "Should be a PDF"
                  // Every PDF starts with this signature.
                  Expect.stringStarts (Encoding.ASCII.GetString(file.Content, 0, 5)) "%PDF-" "Should be PDF content"
              | result -> failtestf "Expected a file result, but got %A" result

          testCase "A calculation without output is an empty list"
          <| fun () -> Expect.equal (calculate "" |> expectOk) [] "Should be empty"

          testCase "A calculation with one item is a list of one"
          <| fun () ->
              Expect.sequenceEqual
                  (calculate "[pscustomobject]@{ Name = 'EGGJ' }" |> expectOk |> List.map toJsonString)
                  [ """{"Name":"EGGJ"}""" ]
                  "A lone object should not collapse into the item itself"

          testCase "A calculation with several items is a list of each"
          <| fun () ->
              Expect.sequenceEqual
                  (calculate "'EGGJ'; 'GRUG'" |> expectOk |> List.map toJsonString)
                  [ "\"EGGJ\""; "\"GRUG\"" ]
                  "Should be one entry per object the script wrote" ]
