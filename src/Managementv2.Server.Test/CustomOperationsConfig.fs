module Managementv2.Server.Test.CustomOperationsConfig

open System.IO
open Managementv2.Server
open Expecto

let tests =
    testList
        "JsonFileCustomOperationsConfig"
        [ testCase "Write then Read round-trips every value kind"
          <| fun () ->
              let path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json")

              try
                  let sut = JsonFileCustomOperationsConfig(path) :> ICustomOperationsConfig

                  let config =
                      Map
                          [ "Url", Text "https://example.com"
                            "Logo", File [| 0uy; 1uy; 2uy; 255uy |]
                            "SokratesCredential", Credential("user", "p@ss!")
                            "SokratesCertificate", ProtectedCertificate([| 10uy; 20uy; 30uy |], "pfx-pw") ]

                  sut.Write config
                  Expect.equal (sut.Read()) config "Should read back exactly what was written"
              finally
                  File.Delete path

          testCase "toJson then ofJson round-trips every value kind"
          <| fun () ->
              let config =
                  Map
                      [ "Url", Text "https://example.com"
                        "Logo", File [| 0uy; 1uy; 255uy |]
                        "Cred", Credential("u", "p")
                        "Cert", ProtectedCertificate([| 9uy; 8uy; 7uy |], "pw") ]

              Expect.equal
                  (CustomOperationsConfig.ofJson (CustomOperationsConfig.toJson config))
                  config
                  "Should round-trip through JSON"

          testCase "toJson emits the documented wire shape"
          <| fun () ->
              let json = CustomOperationsConfig.toJson (Map [ "Cred", Credential("u", "p") ])

              Expect.equal
                  (json.ToJsonString())
                  """{"Cred":{"userName":"u","password":"p"}}"""
                  "Credential should serialize as { userName, password }"

          testCase "Reads a { userName, password } object as a credential"
          <| fun () ->
              let path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json")

              try
                  File.WriteAllText(path, """{ "Cred": { "userName": "u", "password": "p" } }""")
                  let sut = JsonFileCustomOperationsConfig(path) :> ICustomOperationsConfig
                  Expect.equal (sut.Read()) (Map [ "Cred", Credential("u", "p") ]) "Should read as Credential"
              finally
                  File.Delete path

          testCase "Read returns an empty map when the file is missing"
          <| fun () ->
              let sut =
                  JsonFileCustomOperationsConfig(
                      Path.Combine(Path.GetTempPath(), "does-not-exist-" + Path.GetRandomFileName())
                  )
                  :> ICustomOperationsConfig

              Expect.equal (sut.Read()) Map.empty "A missing file should read as an empty map"

          testCase "Reads plain JSON strings as text"
          <| fun () ->
              let path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json")

              try
                  File.WriteAllText(path, """{ "A": "1", "B": "two" }""")
                  let sut = JsonFileCustomOperationsConfig(path) :> ICustomOperationsConfig
                  Expect.equal (sut.Read()) (Map [ "A", Text "1"; "B", Text "two" ]) "Plain strings should read as Text"
              finally
                  File.Delete path

          testCase "Reads a { file } object as a byte array"
          <| fun () ->
              let path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json")

              try
                  let base64 = System.Convert.ToBase64String [| 10uy; 20uy; 30uy |]
                  File.WriteAllText(path, $"""{{ "Cert": {{ "file": "{base64}" }} }}""")
                  let sut = JsonFileCustomOperationsConfig(path) :> ICustomOperationsConfig

                  Expect.equal
                      (sut.Read())
                      (Map [ "Cert", File [| 10uy; 20uy; 30uy |] ])
                      "A { file } object should read as File bytes"
              finally
                  File.Delete path ]