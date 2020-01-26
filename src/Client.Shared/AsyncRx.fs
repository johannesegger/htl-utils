module AsyncRx

open Elmish
open FSharp.Control
open Thoth.Elmish

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
