module WakeUp

open Elmish
open Elmish.Streams
open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open FSharp.Control
open Fulma
open Thoth.Elmish
open Thoth.Json
open Thoth.Fetch

[<Fable.Core.StringEnum>]
type Model =
    | Disabled
    | Ready

type Msg =
    | Disable
    | Enable
    | SendWakeUpCommand

let rec update msg model =
    match msg with
    | Disable -> Disabled
    | Enable -> Ready
    | SendWakeUpCommand -> model

let init = Disabled

let view model dispatch =
    let isSendDisabled =
        match model with
        | Disabled -> true
        | Ready -> false

    Container.container [ Container.Props [ Style [ MarginTop "1rem" ] ] ]
        [
            Content.content [ ]
                [
                    Button.list [ Button.List.IsCentered ]
                        [
                            Button.button
                                [
                                    Button.IsLink
                                    Button.Disabled isSendDisabled
                                    Button.OnClick (fun _evt -> dispatch SendWakeUpCommand)
                                ]
                                [
                                    div [ ClassName "block" ]
                                        [
                                            Icon.icon [] [ Fa.i [ Fa.Solid.Bed ] [] ]
                                            span [] [ str "Wake up PC-EGGJ" ]
                                        ]
                                ]
                        ]
                ]
        ]


let stream authHeader states msgs =
    authHeader
    |> AsyncRx.flatMapLatest (function
        | Some authHeader ->
            let send =
                AsyncRx.defer (fun () ->
                    AsyncRx.ofPromise (promise {
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        return! Fetch.post("/api/wakeup/send", Encode.nil, requestProperties)
                    })
                    |> AsyncRx.map (ignore >> Ok)
                    |> AsyncRx.catch (Error >> AsyncRx.single)
                )
            let responseToast response =
                match response with
                | Ok () ->
                    Toast.toast "Wake up" "Wake up signal successfully sent"
                    |> Toast.success
                | Error (e: exn) ->
                    Toast.toast "Wake up failed" e.Message
                    |> Toast.error
            states
            |> AsyncRx.flatMapLatest (function
                | Ready ->
                    msgs
                    |> AsyncRx.choose (function | SendWakeUpCommand -> Some send | _ -> None)
                    |> AsyncRx.switchLatest
                | _ -> AsyncRx.never ()
            )
            |> AsyncRx.showToast responseToast
            |> AsyncRx.flatMapLatest (fun _ -> AsyncRx.never ()) // ignore send result
            |> AsyncRx.startWith [ Enable ]
        | None -> AsyncRx.single Disable
    )
