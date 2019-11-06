module AddAADTeacherContacts

open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open Fable.Reaction
open Fetch.Types
open FSharp.Control
open Fulma
open Thoth.Fetch
open Thoth.Json

type Model = {
    IsEnabled: bool
    IsImporting: bool
}

type Msg =
    | Enable
    | Disable
    | Import
    | ImportResponse of Result<unit, exn>

let init = {
    IsEnabled = false
    IsImporting = false
}

let update msg model =
    match msg with
    | Enable -> { model with IsEnabled = true }
    | Disable -> { model with IsEnabled = false }
    | Import -> { model with IsImporting = true }
    | ImportResponse (Ok ()) -> { model with IsImporting = false }
    | ImportResponse (Error _e) -> { model with IsImporting = false }

let view model dispatch =
    Container.container [] [
        Section.section [ Section.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ] [
            Button.button
                [
                    Button.Size IsLarge
                    Button.Disabled (not model.IsEnabled)
                    Button.IsLoading model.IsImporting
                    Button.Color IsSuccess
                    Button.OnClick (fun _ev -> dispatch Import)
                ]
                [
                    Icon.icon [] [ Fa.i [ Fa.Solid.IdCard ] [] ]
                    span [] [ str "Add teacher contacts" ]
                ]
        ]
    ]

let stream (authHeader: IAsyncObservable<HttpRequestHeaders option>) (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
    authHeader
    |> AsyncRx.flatMapLatest (function
        | None ->
            AsyncRx.single Disable
        | Some authHeader ->
            [
                msgs
                |> AsyncRx.startWith [ Enable ]

                let import =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofPromise (promise {
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            return! Fetch.post("/api/teachers/add-as-contacts", Encode.nil, Decode.succeed (), requestProperties)
                        })
                        |> AsyncRx.map Ok
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )

                states
                |> AsyncRx.choose (fst >> function | Some Import -> Some import | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showSuccessToast (fun () -> "Add teacher contacts", sprintf "Successfully started adding teacher contacts. This might take some minutes to finish.")
                |> AsyncRx.showErrorToast (fun e -> "Adding teacher contacts failed", e.Message)
                |> AsyncRx.map ImportResponse
            ]
            |> AsyncRx.mergeSeq
    )
