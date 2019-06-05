module ImportTeacherContacts

open Elmish.Streams
open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open FSharp.Control
open Fulma
open Thoth.Elmish
open Thoth.Fetch
open Thoth.Json

type Model =
    | Disabled
    | Ready
    | Sending

type Msg =
    | Disable
    | Enable
    | Import
    | ImportResponse of Result<unit, exn>

let rec update msg model =
    match msg with
    | Disable -> Disabled
    | Enable -> Ready
    | Import -> Sending
    | ImportResponse (Error e) -> Ready
    | ImportResponse (Ok ()) -> Ready

let init = Disabled

let view model dispatch =
    let isImportingDisabled =
        match model with
        | Disabled
        | Sending -> true
        | Ready -> false
    Container.container [ Container.Props [ Style [ MarginTop "1rem" ] ] ]
        [
            Content.content []
                [
                    Button.list [ Button.List.IsCentered ]
                        [
                            Button.button
                                [
                                    Button.IsLink
                                    Button.Disabled isImportingDisabled
                                    Button.OnClick (fun _evt -> dispatch Import)
                                ]
                                [
                                    Icon.icon [] [ Fa.i [ Fa.Solid.IdCard ] [] ]
                                    span [] [ str "Import teacher contacts" ]
                                ]
                        ]
                ]
        ]

let stream authHeader model msgs =
    match authHeader, model with
    | None, Ready ->
        AsyncRx.single Disable
    | Some authHeader, Disabled ->
        AsyncRx.single Enable
    | Some authHeader, Ready
    | Some authHeader, Sending ->
        let importStartedToast =
            Toast.toast "Import teacher contacts" "Import started. This might take some time."
            |> Toast.info

        let import =
            AsyncRx.defer (fun () ->
                AsyncRx.ofPromise (promise {
                    let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                    return! Fetch.post("/api/teachers/import-contacts", Encode.nil, requestProperties)
                })
                |> AsyncRx.map ignore
            )

        let responseToast response =
            match response with
            | Ok () ->
                Toast.toast "Import teacher contacts" "Import successful"
                |> Toast.success
            | Error (e: exn) ->
                Toast.toast "Import teacher contacts failed" e.Message
                |> Toast.error

        msgs
        |> AsyncRx.choose (function | Import -> Some import | _ -> None)
        |> AsyncRx.showToast (fun _ -> importStartedToast)
        |> AsyncRx.switchLatest
        |> AsyncRx.map (ignore >> Ok)
        |> AsyncRx.catch (Error >> AsyncRx.single)
        |> AsyncRx.showToast responseToast
        |> AsyncRx.map ImportResponse
    | None, Disabled
    | None, Sending -> msgs
