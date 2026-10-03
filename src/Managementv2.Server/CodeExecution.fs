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

// Per-run temp directory for File/SshKey values materialized as real files.
// Disposing deletes it, so a run cleans up on every exit (success, error or
// cancellation).
type private SecretsDirectory() =
    let path = IO.Path.Combine(IO.Path.GetTempPath(), Guid.NewGuid().ToString "N")

    do
        IO.Directory.CreateDirectory path |> ignore

        if not (OperatingSystem.IsWindows()) then
            IO.File.SetUnixFileMode(
                path,
                IO.UnixFileMode.UserRead ||| IO.UnixFileMode.UserWrite ||| IO.UnixFileMode.UserExecute
            )

    member _.Path = path

    interface IDisposable with
        member _.Dispose() =
            try
                IO.Directory.Delete(path, recursive = true)
            with _ ->
                ()

type CodeExecution() =

    // The PowerShell modules imported into every session, so their cmdlets are
    // available to scripts: Connect-Sokrates, Get-SokratesTeacher, ... from the
    // Sokrates module and ConvertTo-Pdf from the PDF one.
    let modulePaths =
        [| typeof<SokratesPowerShell.SokratesSession>.Assembly.Location
           typeof<PdfPowerShell.ConvertToPdfCommand>.Assembly.Location |]

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
                    X509CertificateLoader.LoadPkcs12(certificate, password)
                | SshKey(userName, keyFile) ->
                    let sshKey = Collections.Hashtable()
                    sshKey["UserName"] <- userName
                    sshKey["KeyFilePath"] <- writeSecretFile keyFile
                    sshKey

            psConfig.Properties.Add(PSNoteProperty(entry.Key, value))

        psConfig

    let errorText (ps: PowerShell) =
        ps.Streams.Error
        |> Seq.map (fun e -> $"* %O{e.Exception}")
        |> String.concat Environment.NewLine

    // Converts output to JSON in the script's own runspace, so PowerShell's conversion
    // rules apply exactly as they did when the script piped into ConvertTo-Json itself.
    // Only called with output to convert.
    let toJson (runspace: Runspace) (results: PSObject seq) : Result<JsonNode, string> =
        use converter = PowerShell.Create()
        converter.Runspace <- runspace
        converter.AddCommand("ConvertTo-Json").AddParameter("Depth", 64) |> ignore

        // Fed as pipeline input, so a single object converts to a JSON object and
        // several to an array, just as in a single pipeline.
        let converted = converter.Invoke results

        if converter.HadErrors then
            Error(errorText converter)
        else
            converted |> Seq.tryHead |> Option.map (fun r -> JsonNode.Parse(string r)) |> Option.toObj |> Ok

    // A calculate script's output is the list of items to execute, so a lone object is
    // still a list of one.
    let toCalculations (runspace: Runspace) (results: PSObject seq) : Result<JsonNode list, string> =
        if Seq.isEmpty results then
            Ok []
        else
            toJson runspace results
            |> Result.map (fun node ->
                match node with
                | :? JsonArray as items -> List.ofSeq items
                | node -> [ node ])

    // An execute script's output decides the kind of result: a lone file result (from
    // New-FileResult) is a download and a lone string is plain text; anything else is
    // JSON.
    let toExecutionResult (runspace: Runspace) (results: PSObject seq) : Result<ExecutionResult, string> =
        let toJsonResult () =
            toJson runspace results |> Result.map ExecutionResult.Json

        match List.ofSeq results with
        | [] -> Ok ExecutionResult.Empty
        | [ single ] when not (isNull single) ->
            match single.BaseObject with
            | :? ResultFile as file -> Ok(ExecutionResult.File file)
            | :? string as text -> Ok(ExecutionResult.Text text)
            | _ -> toJsonResult ()
        | _ -> toJsonResult ()

    // No shared mutable state: each call gets its own runspace, and the Sokrates
    // module keeps its default session in per-runspace session state, so calls can
    // run concurrently without locking.
    let createRunspace () =
        let initialState = InitialSessionState.CreateDefault()
        initialState.ImportPSModule modulePaths

        // A single cmdlet rather than a module of its own: it lives in this assembly,
        // next to the result type it produces.
        initialState.Commands.Add(SessionStateCmdletEntry("New-FileResult", typeof<NewFileResultCommand>, null))

        let runspace = RunspaceFactory.CreateRunspace initialState
        runspace.Open()
        runspace

    // Runs the script and hands back whatever it wrote. The caller turns that into its
    // own kind of result, while the runspace is still open to convert in.
    let execute
        (runspace: Runspace)
        (code: string)
        (input: JsonNode option)
        (config: PSObject)
        (cancellationToken: CancellationToken)
        : Task<Result<PSObject seq, string>> =
        task {
            use ps = PowerShell.Create()
            ps.Runspace <- runspace
            ps.AddScript code |> ignore

            ps.AddParameter("Config", config) |> ignore

            // Pass the JSON input to the script's param block as a PSCustomObject.
            match input with
            | Some node ->
                use converter = PowerShell.Create()
                converter.Runspace <- runspace

                let inputObject =
                    converter.AddCommand("ConvertFrom-Json").AddParameter("InputObject", JsonSerializer.Serialize node).Invoke()

                ps.AddParameter("InputData", inputObject) |> ignore
            | None -> ()

            // InvokeAsync runs the pipeline off the request thread. Cancellation still
            // works by stopping the pipeline, which either faults the task or leaves the
            // invocation state Stopped; both are handled below.
            use _registration = cancellationToken.Register(fun () -> ps.Stop())

            try
                let! results = ps.InvokeAsync()

                if ps.InvocationStateInfo.State = PSInvocationState.Stopped then
                    return raise (OperationCanceledException cancellationToken)
                elif ps.HadErrors then
                    return Error(errorText ps)
                else
                    return Ok(results :> PSObject seq)
            with
            // A stop surfaces as either of these; treat it as cancellation, not an error.
            | :? OperationCanceledException -> return raise (OperationCanceledException cancellationToken)
            | :? PipelineStoppedException -> return raise (OperationCanceledException cancellationToken)
            | e -> return Error(e.ToString())
        }

    /// Runs a calculate script, whose output is the list of items to execute.
    member _.ExecuteCalculation config code (cancellationToken: CancellationToken) =
        task {
            cancellationToken.ThrowIfCancellationRequested()

            // Declared first, so the secrets are removed after the runspace is torn down.
            use secrets = new SecretsDirectory()
            use runspace = createRunspace ()

            let executionConfig = buildConfig secrets.Path config
            let! results = execute runspace code None executionConfig cancellationToken
            return results |> Result.bind (toCalculations runspace)
        }

    /// Runs an execute script for one item.
    member _.ExecuteOperation config code data (cancellationToken: CancellationToken) =
        task {
            cancellationToken.ThrowIfCancellationRequested()

            // Declared first, so the secrets are removed after the runspace is torn down.
            use secrets = new SecretsDirectory()
            use runspace = createRunspace ()

            let executionConfig = buildConfig secrets.Path config
            let! results = execute runspace code (Some data) executionConfig cancellationToken
            return results |> Result.bind (toExecutionResult runspace)
        }