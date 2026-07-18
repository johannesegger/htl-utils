namespace SokratesPowerShell

open System
open System.Management.Automation
open Sokrates

/// An authenticated Sokrates connection. Wraps the underlying SokratesApi plus a
/// human-readable description. Can be built from an already-configured SokratesApi
/// (e.g. by a hosting application) or via Connect-Sokrates.
[<AllowNullLiteral>]
type SokratesSession(api: SokratesApi, description: string) =
    new(api: SokratesApi) = SokratesSession(api, "Sokrates session")
    member _.Api = api
    member _.Description = description
    override _.ToString() = description

/// Module-scoped default session set by Connect-Sokrates. Cmdlets fall back to it
/// when no explicit -Session is given.
[<RequireQualifiedAccess>]
module internal SessionState =
    let mutable Current: SokratesSession option = None

/// Base class for cmdlets that operate on a Sokrates session. Adds the optional
/// -Session parameter and resolves the effective session (explicit -Session first,
/// otherwise the connected default).
[<AbstractClass>]
type SokratesCmdlet() =
    inherit PSCmdlet()

    [<Parameter>]
    member val Session: SokratesSession = null with get, set

    member this.ResolveSession() : SokratesSession =
        match this.Session with
        | null ->
            match SessionState.Current with
            | Some session -> session
            | None ->
                let ex =
                    InvalidOperationException(
                        "No Sokrates session available. Run Connect-Sokrates first, or pass -Session."
                    )

                this.ThrowTerminatingError(ErrorRecord(ex, "NoSokratesSession", ErrorCategory.ConnectionError, null))
                Unchecked.defaultof<SokratesSession>
        | session -> session

    /// Runs an async computation that returns a list and streams each element to the pipeline.
    member this.WriteAll(computation: Async<'T list>) =
        let results = Async.RunSynchronously computation
        this.WriteObject(results, true)
