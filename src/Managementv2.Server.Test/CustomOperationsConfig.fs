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
                  let sut = JsonFileCustomOperationsConfig path :> ICustomOperationsConfig

                  let config =
                      Map
                          [ "Url", ConfigEntry.ofValue (Text "https://example.com")
                            "Logo", ConfigEntry.ofValue (File [| 0uy; 1uy; 2uy; 255uy |])
                            "SokratesCredential", ConfigEntry.ofValue (Credential("user", "p@ss!"))
                            "SokratesCertificate",
                            ConfigEntry.ofValue (ProtectedCertificate([| 10uy; 20uy; 30uy |], "pfx-pw")) ]

                  sut.Write config
                  Expect.equal (sut.Read()) config "Should read back exactly what was written"
              finally
                  File.Delete path

          testCase "Write then Read round-trips comments on every value kind"
          <| fun () ->
              let path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json")

              try
                  let sut = JsonFileCustomOperationsConfig path :> ICustomOperationsConfig

                  let config =
                      Map
                          [ "Url", { Value = Text "https://example.com"; Comment = "The staging host" }
                            "Logo", { Value = File [| 1uy; 2uy |]; Comment = "Exported from the design file" }
                            "Cred", { Value = Credential("user", "p@ss!"); Comment = "Rotates every year" }
                            "Cert", { Value = ProtectedCertificate([| 3uy |], "pw"); Comment = "Expires 2027" }
                            "Ssh", { Value = SshKey("user", [| 4uy |]); Comment = "Deploy key" } ]

                  sut.Write config
                  Expect.equal (sut.Read()) config "Comments should survive a write/read round-trip"
              finally
                  File.Delete path

          testCase "toJson then ofJson round-trips every value kind"
          <| fun () ->
              let config =
                  Map
                      [ "Url", ConfigEntry.ofValue (Text "https://example.com")
                        "Logo", ConfigEntry.ofValue (File [| 0uy; 1uy; 255uy |])
                        "Cred", ConfigEntry.ofValue (Credential("u", "p"))
                        "Cert", ConfigEntry.ofValue (ProtectedCertificate([| 9uy; 8uy; 7uy |], "pw")) ]

              Expect.equal
                  (CustomOperationsConfig.ofJson (CustomOperationsConfig.toJson config))
                  config
                  "Should round-trip through JSON"

          testCase "toJson emits the documented wire shape"
          <| fun () ->
              let json =
                  CustomOperationsConfig.toJson (Map [ "Cred", ConfigEntry.ofValue (Credential("u", "p")) ])

              Expect.equal
                  (json.ToJsonString())
                  """{"Cred":{"userName":"u","password":"p","comment":""}}"""
                  "Credential should serialize as { userName, password, comment }"

          testCase "toJson appends the comment to an existing object shape"
          <| fun () ->
              let json =
                  CustomOperationsConfig.toJson (Map [ "Cred", { Value = Credential("u", "p"); Comment = "note" } ])

              Expect.equal
                  (json.ToJsonString())
                  """{"Cred":{"userName":"u","password":"p","comment":"note"}}"""
                  "A comment should be an extra property of the value object"

          testCase "toJson writes text as an object, with a blank comment when there is none"
          <| fun () ->
              let json = CustomOperationsConfig.toJson (Map [ "A", ConfigEntry.ofValue (Text "1") ])

              Expect.equal
                  (json.ToJsonString())
                  """{"A":{"text":"1","comment":""}}"""
                  "Text should always carry a comment property"

          testCase "toJson writes a commented text value as { text, comment }"
          <| fun () ->
              let json = CustomOperationsConfig.toJson (Map [ "A", { Value = Text "1"; Comment = "note" } ])

              Expect.equal
                  (json.ToJsonString())
                  """{"A":{"text":"1","comment":"note"}}"""
                  "Commented text should serialize as { text, comment }"

          testCase "Reads a { text, comment } object as commented text"
          <| fun () ->
              let config =
                  CustomOperationsConfig.ofJson (
                      System.Text.Json.Nodes.JsonNode.Parse """{ "A": { "text": "1", "comment": "note" } }"""
                  )

              Expect.equal
                  config
                  (Map [ "A", { Value = Text "1"; Comment = "note" } ])
                  "Should read back as Text with a comment"

          testCase "Reads an entry without a comment property as a blank comment"
          <| fun () ->
              let config =
                  CustomOperationsConfig.ofJson (System.Text.Json.Nodes.JsonNode.Parse """{ "A": { "text": "1" } }""")

              Expect.equal
                  config
                  (Map [ "A", ConfigEntry.ofValue (Text "1") ])
                  "A missing comment should read as blank"

          testCase "Reads a { userName, password } object as a credential"
          <| fun () ->
              let path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json")

              try
                  File.WriteAllText(path, """{ "Cred": { "userName": "u", "password": "p" } }""")
                  let sut = JsonFileCustomOperationsConfig path :> ICustomOperationsConfig

                  Expect.equal
                      (sut.Read())
                      (Map [ "Cred", ConfigEntry.ofValue (Credential("u", "p")) ])
                      "Should read as Credential"
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
                  let sut = JsonFileCustomOperationsConfig path :> ICustomOperationsConfig

                  Expect.equal
                      (sut.Read())
                      (Map [ "A", ConfigEntry.ofValue (Text "1"); "B", ConfigEntry.ofValue (Text "two") ])
                      "Plain strings should read as Text"
              finally
                  File.Delete path

          testCase "Reads a { file } object as a byte array"
          <| fun () ->
              let path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json")

              try
                  let base64 = System.Convert.ToBase64String [| 10uy; 20uy; 30uy |]
                  File.WriteAllText(path, $"""{{ "Cert": {{ "file": "{base64}" }} }}""")
                  let sut = JsonFileCustomOperationsConfig path :> ICustomOperationsConfig

                  Expect.equal
                      (sut.Read())
                      (Map [ "Cert", ConfigEntry.ofValue (File [| 10uy; 20uy; 30uy |]) ])
                      "A { file } object should read as File bytes"
              finally
                  File.Delete path ]
