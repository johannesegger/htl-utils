namespace Managementv2.Server

open System
open System.Management.Automation
open System.Management.Automation.Runspaces
open System.Security
open System.Security.Cryptography.X509Certificates
open System.Text.Json.Nodes

type CodeExecution() =
    let gate = obj ()

    // Path to the Sokrates PowerShell module, imported into every session so its
    // cmdlets (Connect-Sokrates, Get-SokratesTeacher, ...) are available to scripts.
    let sokratesModulePath =
        typeof<SokratesPowerShell.SokratesSession>.Assembly.Location

    let toSecureString (text: string) =
        let secure = new SecureString()

        if not (isNull text) then
            text |> Seq.iter secure.AppendChar

        secure.MakeReadOnly()
        secure

    // The $Config object passed to every script, built from the operations config
    // (read by the caller) so the secrets never live in the operation scripts. Each
    // config property becomes a property of $Config under the same name, projected to
    // the natural PowerShell type for its kind.
    let buildConfig (config: Map<string, ConfigValue>) =
        let psConfig = PSObject()

        for entry in config do
            let value: obj =
                match entry.Value with
                | Text text -> text
                | File bytes -> bytes
                | Credential(userName, password) -> PSCredential(userName, toSecureString password)
                | ProtectedCertificate(certificate, password) ->
                    X509CertificateLoader.LoadPkcs12(certificate, password, X509KeyStorageFlags.Exportable)

            psConfig.Properties.Add(PSNoteProperty(entry.Key, value))

        psConfig

    let execute (code: string) (input: JsonNode option) (config: Map<string, ConfigValue>) =
        lock gate (fun () ->
            let initialState = InitialSessionState.CreateDefault()
            initialState.ImportPSModule [| sokratesModulePath |]
            use runspace = RunspaceFactory.CreateRunspace(initialState)
            runspace.Open()

            use ps = PowerShell.Create()
            ps.Runspace <- runspace
            ps.AddScript code |> ignore

            // Pass the secrets to the script's -Config parameter.
            ps.AddParameter("Config", buildConfig config) |> ignore

            // Pass the JSON input to the script's param block as a PSCustomObject.
            match input with
            | Some node ->
                use converter = PowerShell.Create()
                converter.Runspace <- runspace

                let inputObject =
                    converter.AddCommand("ConvertFrom-Json").AddParameter("InputObject", node.ToJsonString()).Invoke()

                ps.AddParameter("InputData", inputObject) |> ignore
            | None -> ()

            // Convert the script's output to JSON so it can be returned as a JsonNode.
            ps.AddCommand("ConvertTo-Json").AddParameter("Depth", 64) |> ignore

            try
                let results = ps.Invoke()

                if ps.HadErrors then
                    let errorText =
                        ps.Streams.Error
                        |> Seq.map (fun e -> $"* %O{e.Exception}")
                        |> String.concat Environment.NewLine

                    Error errorText
                else
                    results |> Seq.tryHead |> Option.map (fun r -> JsonNode.Parse(string r)) |> Ok
            with e ->
                Error(e.ToString()))

    member _.Execute config code = execute code None config

    member _.ExecuteWithInput config code data = execute code (Some data) config