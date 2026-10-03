module Managementv2.Server.Test.ExecutionResult

open Managementv2.Server
open System.Text.Json.Nodes
open Expecto

let private toJsonString result =
    (ExecutionResult.toJson result).ToJsonString()

let tests =
    testList
        "ExecutionResult.toJson"
        [ testCase "Encodes an empty result"
          <| fun () -> Expect.equal (toJsonString ExecutionResult.Empty) """{"kind":"empty"}""" "Should only carry the kind"

          testCase "Encodes a text result"
          <| fun () ->
              Expect.equal
                  (toJsonString (ExecutionResult.Text "User EGGJ created"))
                  """{"kind":"text","text":"User EGGJ created"}"""
                  "Should carry the text as-is"

          testCase "Encodes a json result"
          <| fun () ->
              Expect.equal
                  (toJsonString (ExecutionResult.Json(JsonNode.Parse """[{"Name":"EGGJ"}]""")))
                  """{"kind":"json","data":[{"Name":"EGGJ"}]}"""
                  "Should nest the script's output under data"

          testCase "Encodes a file result with base64 content"
          <| fun () ->
              let file =
                  { Name = "report.pdf"
                    ContentType = "application/pdf"
                    Content = [| 10uy; 20uy; 30uy |] }

              Expect.equal
                  (toJsonString (ExecutionResult.File file))
                  """{"kind":"file","name":"report.pdf","contentType":"application/pdf","content":"ChQe"}"""
                  "Should base64-encode the content"

          testCase "Guesses the content type from the file name"
          <| fun () ->
              Expect.equal (ContentType.ofFileName "report.pdf") "application/pdf" "Should map a known extension"

          testCase "Falls back to octet-stream for an unknown extension"
          <| fun () ->
              Expect.equal
                  (ContentType.ofFileName "report.whatever")
                  "application/octet-stream"
                  "Should fall back for an unknown extension" ]
