module Async

open System

let retn v = async { return v }

let bind fn a = async {
    let! v = a
    return! fn v
}

let map fn = bind (fn >> retn)

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
