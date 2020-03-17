module AsyncRx

open Elmish
open FSharp.Control
open FSharp.Control.Core
open Thoth.Elmish

// TODO remove if https://github.com/dbrattli/Fable.Reaction/pull/64 is merged
let ofAsync' wf =
    AsyncRx.ofAsyncWorker(fun obv _ -> async {
        try
            let! result = wf
            do! obv.OnNextAsync result
            do! obv.OnCompletedAsync ()
        with
        | ex ->
            do! obv.OnErrorAsync ex
    })

let private transformAsync mapNextAsync (source: IAsyncObservable<_>) =
    let subscribeAsync (aobv : IAsyncObserver<'TResult>) =
        { new IAsyncObserver<'TSource> with
            member __.OnNextAsync x = mapNextAsync aobv.OnNextAsync x
            member __.OnErrorAsync err = aobv.OnErrorAsync err
            member __.OnCompletedAsync () = aobv.OnCompletedAsync ()
        }
        |> source.SubscribeAsync
    AsyncRx.create subscribeAsync

// TODO remove if https://github.com/dbrattli/Fable.Reaction/pull/62 is merged
let skip n =
    let mutable remaining = n
    transformAsync (fun onNextAsync item -> async {
        if remaining <= 0 then
            do! onNextAsync item
        else
            remaining <- remaining - 1
    })

// TODO remove if https://github.com/dbrattli/Fable.Reaction/pull/63 is merged
let take' (count: int) (source: IAsyncObservable<'TSource>) : IAsyncObservable<'TSource> =
    let subscribeAsync (obvAsync : IAsyncObserver<'TSource>) =
        let safeObv, autoDetach = autoDetachObserver obvAsync

        async {
            let mutable remaining = count

            let _obv (n : Notification<'TSource>) : Async<unit> =
                match n, remaining with
                | OnNext x, n when n > 1 ->
                    remaining <- n - 1
                    safeObv.OnNextAsync x
                | OnNext x, n when n = 1 ->
                    async {
                        remaining <- 0
                        do! safeObv.OnNextAsync x
                        do! safeObv.OnCompletedAsync ()
                    }
                | OnNext _, _ -> Async.empty
                | OnError ex, _ -> safeObv.OnErrorAsync ex
                | OnCompleted, _ -> safeObv.OnCompletedAsync ()

            return! source.SubscribeAsync (AsyncObserver.Create _obv) |> autoDetach
        }
    { new IAsyncObservable<'TSource> with member __.SubscribeAsync o = subscribeAsync o }

let awaitLast (source: IAsyncObservable<_>) =
    Async.FromContinuations (fun (cont, econt, ccont) ->
        let mutable result = None
        { new IAsyncObserver<'TSource> with
            member __.OnNextAsync x = async { result <- Some x }
            member __.OnErrorAsync err = async { econt err }
            member __.OnCompletedAsync () = async {
                match result with
                | Some v -> cont v
                | None -> econt (exn "Sequence was empty")
            }
        }
        |> source.SubscribeAsync
        |> Async.Ignore
        |> Async.StartImmediate
    )

let private showToast (toast: 'a -> Cmd<_>) =
    AsyncRx.tapOnNext (fun e ->
        toast e |> List.iter (fun sub -> sub ignore)
    )

let showErrorToast (errorFn: 'a -> Toast.Builder<'b, 'c>) =
    let fn = function
        | Ok _ -> Cmd.none
        | Error e ->
            errorFn e
            |> Toast.error
    showToast fn

let showSuccessToast (successFn: 'a -> Toast.Builder<'b, 'c>) =
    let fn = function
        | Ok v ->
            successFn v
            |> Toast.success
        | Error _ -> Cmd.none
    showToast fn

let showSimpleErrorToast (errorFn: 'a -> string * string) =
    showErrorToast (fun e ->
        let (title, message) = errorFn e
        Toast.create message
        |> Toast.title title
    )

let showSimpleSuccessToast (successFn: 'a -> string * string) =
    showSuccessToast (fun e ->
        let (title, message) = successFn e
        Toast.create message
        |> Toast.title title
    )
