namespace Managementv2.Server

open System
open System.Management.Automation
open System.Management.Automation.Runspaces
open System.Security
open System.Security.Cryptography.X509Certificates
open System.Text.Json
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks

type CodeExecution() =

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
    // the natural PowerShell type for its kind. File-backed values are materialized as
    // real files under secretsDirectory (owner-only permissions) and surfaced as paths.
    let buildConfig (secretsDirectory: string) (config: Map<string, ConfigValue>) =
        // Random file names: a config key never influences the path, so a value can
        // never escape the per-run directory or collide with another.
        let writeSecretFile (bytes: byte[]) =
            let path = IO.Path.Combine(secretsDirectory, Guid.NewGuid().ToString "N")
            IO.File.WriteAllBytes(path, bytes)

            // 0600 — required for SSH private keys, and the right default for any secret.
            if not (OperatingSystem.IsWindows()) then
                IO.File.SetUnixFileMode(path, IO.UnixFileMode.UserRead ||| IO.UnixFileMode.UserWrite)

            path

        let psConfig = PSObject()

        for entry in config do
            let value: obj =
                match entry.Value with
                | Text text -> text
                | File bytes -> writeSecretFile bytes
                | Credential(userName, password) -> PSCredential(userName, toSecureString password)
                | ProtectedCertificate(certificate, password) ->
                    X509CertificateLoader.LoadPkcs12(certificate, password, X509KeyStorageFlags.Exportable)

            psConfig.Properties.Add(PSNoteProperty(entry.Key, value))

        psConfig

    // No shared mutable state: each call gets its own runspace, and the Sokrates
    // module keeps its default session in per-runspace session state, so calls can
    // run concurrently without locking.
    let execute
        (code: string)
        (input: JsonNode option)
        (config: Map<string, ConfigValue>)
        (cancellationToken: CancellationToken)
        : Task<Result<JsonNode option, string>> =
        task {
            cancellationToken.ThrowIfCancellationRequested()

            // Per-run temp directory for File/SshKey values materialized as real files.
            // The disposable deletes it on every exit (success, error or cancellation),
            // and being declared first it runs after the runspace is torn down.
            let secretsDirectory = IO.Path.Combine(IO.Path.GetTempPath(), Guid.NewGuid().ToString "N")
            IO.Directory.CreateDirectory secretsDirectory |> ignore

            if not (OperatingSystem.IsWindows()) then
                IO.File.SetUnixFileMode(
                    secretsDirectory,
                    IO.UnixFileMode.UserRead ||| IO.UnixFileMode.UserWrite ||| IO.UnixFileMode.UserExecute
                )

            use _secrets =
                { new IDisposable with
                    member _.Dispose() =
                        try
                            IO.Directory.Delete(secretsDirectory, recursive = true)
                        with _ ->
                            () }

            let initialState = InitialSessionState.CreateDefault()
            initialState.ImportPSModule [| sokratesModulePath |]
            use runspace = RunspaceFactory.CreateRunspace(initialState)
            runspace.Open()

            use ps = PowerShell.Create()
            ps.Runspace <- runspace
            ps.AddScript code |> ignore

            // Pass the secrets to the script's -Config parameter.
            ps.AddParameter("Config", buildConfig secretsDirectory config) |> ignore

            // Pass the JSON input to the script's param block as a PSCustomObject.
            match input with
            | Some node ->
                use converter = PowerShell.Create()
                converter.Runspace <- runspace

                let inputObject =
                    converter.AddCommand("ConvertFrom-Json").AddParameter("InputObject", JsonSerializer.Serialize node).Invoke()

                ps.AddParameter("InputData", inputObject) |> ignore
            | None -> ()

            // Convert the script's output to JSON so it can be returned as a JsonNode.
            ps.AddCommand("ConvertTo-Json").AddParameter("Depth", 64) |> ignore

            // InvokeAsync runs the pipeline off the request thread. Cancellation still
            // works by stopping the pipeline, which either faults the task or leaves the
            // invocation state Stopped; both are handled below.
            use _registration = cancellationToken.Register(fun () -> ps.Stop())

            try
                let! results = ps.InvokeAsync()

                if ps.InvocationStateInfo.State = PSInvocationState.Stopped then
                    return raise (OperationCanceledException(cancellationToken))
                elif ps.HadErrors then
                    let errorText =
                        ps.Streams.Error
                        |> Seq.map (fun e -> $"* %O{e.Exception}")
                        |> String.concat Environment.NewLine

                    return Error errorText
                else
                    return results |> Seq.tryHead |> Option.map (fun r -> JsonNode.Parse(string r)) |> Ok
            with
            // A stop surfaces as either of these; treat it as cancellation, not an error.
            | :? OperationCanceledException -> return raise (OperationCanceledException(cancellationToken))
            | :? PipelineStoppedException -> return raise (OperationCanceledException(cancellationToken))
            | e -> return Error(e.ToString())
        }

    member _.Execute config code cancellationToken =
        execute code None config cancellationToken

    member _.ExecuteWithInput config code data cancellationToken =
        execute code (Some data) config cancellationToken