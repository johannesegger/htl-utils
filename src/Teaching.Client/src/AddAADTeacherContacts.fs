module AddAADTeacherContacts

open Fable.Core
open Fable.FontAwesome
open Fable.React
open Fable.Reaction
open FSharp.Control
open Fulma
open Thoth.Fetch
open Thoth.Json

type Model = {
    IsImporting: bool
}

type Msg =
    | Import
    | ImportResponse of Result<unit, exn>

let init = {
    IsImporting = false
}

let update msg model =
    match msg with
    | Import -> { model with IsImporting = true }
    | ImportResponse (Ok ()) -> { model with IsImporting = false }
    | ImportResponse (Error _e) -> { model with IsImporting = false }

let view model dispatch =
    Container.container [] [
        Section.section [ Section.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ] [
            Button.button
                [
                    Button.Size IsLarge
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

let stream getAuthRequestHeader (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
    [
        msgs

        let import =
            AsyncRx.defer (fun () ->
                AsyncRx.ofAsync (async {
                    let! authHeader = getAuthRequestHeader ()
                    let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                    // TODO Replace with XHR because fetch doesn't support increasing timeouts; Some browser trigger a timeout after about 2 minutes
                    return! Fetch.post("/api/teachers/add-as-contacts", Encode.nil, Decode.succeed (), requestProperties) |> Async.AwaitPromise
                })
                |> AsyncRx.map Ok
                |> AsyncRx.catch (Error >> AsyncRx.single)
            )

        states
        |> AsyncRx.choose (fst >> function | Some Import -> Some import | _ -> None)
        |> AsyncRx.switchLatest
        |> AsyncRx.showSimpleSuccessToast (fun () -> "Add teacher contacts", sprintf "Successfully added teacher contacts.")
        |> AsyncRx.showSimpleErrorToast (fun e -> "Adding teacher contacts failed", e.Message)
        |> AsyncRx.map ImportResponse
    ]
    |> AsyncRx.mergeSeq
