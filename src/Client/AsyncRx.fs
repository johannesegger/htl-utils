namespace global

module AsyncRx =
    open Elmish
    open FSharp.Control

    // let ofCmd (cmd: Cmd<_>) =
    //     AsyncRx.create (fun obs -> async {
    //         cmd |> List.iter (fun sub -> sub (obs.OnNextAsync >> Async.Start)) // TODO not sure why this is `OnNextAsync`
    //         return AsyncDisposable.Empty
    //     })

    let showToast (toast: 'a -> Cmd<_>) =
        AsyncRx.tapOnNext (fun e ->
            toast e |> List.iter (fun sub -> sub ignore)
        )

module Stream =
    open Elmish.Streams

    let merge s2 s1 = Stream.batch [ s1; s2 ]
