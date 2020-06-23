module AsyncRx

open Elmish
open FSharp.Control
open FSharp.Control.Core
open Thoth.Elmish

let private transformAsync mapNextAsync (source: IAsyncObservable<_>) =
    let subscribeAsync (aobv : IAsyncObserver<'TResult>) =
        { new IAsyncObserver<'TSource> with
            member __.OnNextAsync x = mapNextAsync aobv.OnNextAsync x
            member __.OnErrorAsync err = aobv.OnErrorAsync err
            member __.OnCompletedAsync () = aobv.OnCompletedAsync ()
        }
        |> source.SubscribeAsync
    AsyncRx.create subscribeAsync

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
