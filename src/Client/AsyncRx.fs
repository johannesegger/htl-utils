namespace global

module AsyncRx =
    open Elmish
    open FSharp.Control

    let showToast (toast: 'a -> Cmd<_>) =
        AsyncRx.tapOnNext (fun e ->
            toast e |> List.iter (fun sub -> sub ignore)
        )
