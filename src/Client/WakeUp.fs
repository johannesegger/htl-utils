module WakeUp

open Elmish
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack
open Fable.PowerPack.Fetch
open Fulma
open Fulma.FontAwesome
open Thoth.Elmish

type Model = unit

type Msg =
    | SendWakeUpCommand
    | SendWakeUpResponse of Result<unit, exn>

let rec update authHeaderOptFn msg model =
    match msg with
    | SendWakeUpCommand ->
        match authHeaderOptFn with
        | Some getAuthHeader ->
            let cmd =
                Cmd.ofPromise
                    (getAuthHeader >> Promise.bind (List.singleton >> requestHeaders >> List.singleton >> postRecord "/api/wakeup/send" ()))
                    ()
                    (ignore >> Ok >> SendWakeUpResponse)
                    (Error >> SendWakeUpResponse)
            model, cmd
        | None ->
            let msg = exn "Please sign in using your Microsoft account." |> Error |> SendWakeUpResponse
            update authHeaderOptFn msg model
    | SendWakeUpResponse (Error e) ->
        let cmd =
            Toast.toast "Wake up failed" e.Message
            |> Toast.error
        model, cmd
    | SendWakeUpResponse (Ok ()) ->
        let cmd =
            Toast.toast "Wake up" "Wake up signal successfully sent"
            |> Toast.success
        model, cmd

let init() =
    (), Cmd.none

let view model dispatch =
    Container.container [ Container.Props [ Style [ MarginTop "1rem" ] ] ]
        [ Content.content [ ]
            [ Button.list [ Button.List.IsCentered ]
                [ Button.button
                    [ Button.IsLink
                      Button.OnClick (fun _evt -> dispatch SendWakeUpCommand) ]
                    [ Icon.faIcon [ Icon.Size IsSmall ]
                        [ Fa.icon Fa.I.Bed ]
                      span [] [ str "Wake up PC-EGGJ" ] ] ] ] ]
