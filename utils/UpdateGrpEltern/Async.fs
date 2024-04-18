module Async

open System

let retryUntilTrue wf = async {
    let mutable isDone = false
    while not isDone do
        let! result = wf
        isDone <- result
}

let rec retryIfError cont wf = async {
    match! Async.Catch wf with
    | Choice1Of2 result -> return result
    | Choice2Of2 e ->
        match! cont with
        | true ->
            printfn $"WARNING: Retrying operation because it failed with the following error message: %s{e.Message}"
            return! retryIfError cont wf
        | false -> return raise e
}

let rec retryIfErrorWithTimeout (timeout: TimeSpan) =
    let cont = async {
        do! Async.Sleep timeout
        return true
    }
    retryIfError cont
