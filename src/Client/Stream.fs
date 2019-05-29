namespace Smaerts

open Elmish
open FSharp.Control

[<RequireQualifiedAccess>]
module Program =
    let withStream (stream: IAsyncObservable<_> -> IAsyncObservable<_> -> IAsyncObservable<_>) (program: Elmish.Program<_,_,_,_>) =
        let (modelObserver, modelObservable) = AsyncRx.subject ()
        let (messageObserver, messageObservable) = AsyncRx.subject ()
        let messages = stream modelObservable messageObservable

        let mutable dispatch = ignore

        let msgObserver =
            { new IAsyncObserver<'msg> with
                member __.OnNextAsync x = async {
                    dispatch x
                }
                member __.OnErrorAsync err = async {
                    Browser.Dom.console.error ("[Smaerts] Stream error", err)
                }
                member __.OnCompletedAsync () = async {
                    Browser.Dom.console.log ("[Smaerts] Stream completed.")
                }
            }

        let mutable initState = None
        let init' fn arg =
            let (model, cmd) = fn arg
            initState <- Some model
            (model, cmd)

        let update' fn msg model =
            let (model, cmd) = fn msg model
            modelObserver.OnNextAsync model |> Async.StartImmediate
            (model, cmd)

        let mutable hasSubscription = 0
        let mutable subscription = AsyncDisposable.Empty
        let view' fn model dispatch' =
            dispatch <- dispatch'

            // Wait with subscribing until we have a `dispatch`, otherwise `startWith` messages would get lost
#if FABLE_COMPILER
            if hasSubscription = 0 then
                hasSubscription <- 1
#else
            if System.Threading.Interlocked.CompareExchange(&hasSubscription, 1, 0) = 0 then
#endif
                async {
                    let! sub = messages.SubscribeAsync msgObserver
                    subscription <- sub
                }
                |> Async.StartImmediate
                initState |> Option.iter (modelObserver.OnNextAsync >> Async.StartImmediate)
                initState <- None
            fn model (messageObserver.OnNextAsync >> Async.StartImmediate)

        program
        |> Program.map init' update' view' id id
