module WakeUp

open Elmish
open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open Fulma
open Thoth.Elmish
open Thoth.Json
open Thoth.Fetch

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
                Cmd.OfPromise.either
                    (fun getAuthHeader -> promise {
                        let! authHeader = getAuthHeader ()
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        return! Fetch.post("/api/wakeup/send", Encode.nil, requestProperties)
                    })
                    getAuthHeader
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
                    [ div [ ClassName "block" ]
                        [ Icon.icon [] [ Fa.i [ Fa.Solid.Bed ] [] ]
                          span [] [ str "Wake up PC-EGGJ" ] ] ] ] ] ]
