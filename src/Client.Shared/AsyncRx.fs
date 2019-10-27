module AsyncRx

open Elmish
open FSharp.Control
open Thoth.Elmish

let private showToast (toast: 'a -> Cmd<_>) =
    AsyncRx.tapOnNext (fun e ->
        toast e |> List.iter (fun sub -> sub ignore)
    )

let private toast title message =
    Toast.message message
    |> Toast.title title
    |> Toast.position Toast.BottomRight
    |> Toast.noTimeout
    |> Toast.withCloseButton
    |> Toast.dismissOnClick

let showErrorToast (errorFn: 'a -> string * string) =
    let fn = function
        | Ok _ -> Cmd.none
        | Error e ->
            let (title, message) = errorFn e
            toast title message
            |> Toast.error
    showToast fn

let showSuccessToast (successFn: 'a -> string * string) =
    let fn = function
        | Ok v ->
            let (title, message) = successFn v
            toast title message
            |> Toast.success
        | Error _ -> Cmd.none
    showToast fn
