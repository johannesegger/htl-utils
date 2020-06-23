module WakeUp

open Fable.Core
open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open Fable.Reaction
open Fetch.Types
open FSharp.Control
open Fulma
open Thoth.Fetch
open Thoth.Json

type State = Normal | Editing | Sending

type Model = {
    MacAddress: string * string option
    State: State
}

type Msg =
    | BeginEdit
    | UpdateMacAddress of string
    | EndEdit
    | SendWakeUp
    | SendWakeUpResponse of Result<unit, exn>

let init = {
    MacAddress = "", None
    State = Normal
}

let validateMacAddress (value: string) =
    if System.String.IsNullOrWhiteSpace value
    then None
    else Some value

let update msg model =
    match msg with
    | BeginEdit -> { model with State = Editing }
    | UpdateMacAddress value -> { model with MacAddress = (value, validateMacAddress value) }
    | EndEdit -> { model with State = Normal }
    | SendWakeUp -> { model with State = Sending }
    | SendWakeUpResponse (Ok ()) -> { model with State = Normal }
    | SendWakeUpResponse (Error _e) -> { model with State = Normal }

let view model dispatch =
    let tagButton cssClass props children =
        Button.button
            [
                Button.CustomClass (sprintf "tag %s is-large" cssClass)
                Button.Props [ Style [ PaddingTop "0.25em" ] ]
                yield! props
            ]
            children
    Container.container [] [
        Section.section [] [
            Tag.list [ Tag.List.HasAddons; Tag.List.IsCentered ] [
                Tag.tag [ Tag.Color IsInfo; Tag.Size IsLarge ] [ str "Wake up" ]
                match model.State with
                | Editing ->
                    Tag.tag [ Tag.Size IsLarge ] [
                        Input.text [
                            Input.Value (fst model.MacAddress)
                            Input.OnChange (fun ev -> dispatch (UpdateMacAddress ev.Value))
                            Input.Props [ AutoFocus true ]
                        ]
                    ]
                    tagButton "is-warning" [ Button.OnClick (fun ev -> dispatch EndEdit ) ]
                        [
                            Icon.icon [] [ Fa.i [ Fa.Solid.Check ] [] ]
                        ]
                | Normal
                | Sending ->
                    Tag.tag [ Tag.Size IsLarge ] [ str (model.MacAddress |> snd |> Option.defaultValue "<No MAC address specified>") ]
                    tagButton "is-warning"
                        [
                            Button.OnClick (fun _ev -> dispatch BeginEdit)
                            Button.Disabled (model.State = Sending)
                        ]
                        [
                            Icon.icon [] [ Fa.i [ Fa.Solid.PencilAlt ] [] ]
                        ]
                tagButton "is-success"
                    [
                        Button.Disabled (snd model.MacAddress |> Option.isNone || model.State = Editing)
                        Button.OnClick (fun _ev -> dispatch SendWakeUp)
                        Button.IsLoading (model.State = Sending)
                    ]
                    [
                        Icon.icon [] [ Fa.i [ Fa.Solid.PaperPlane ] [] ]
                    ]
            ]
        ]
    ]

let stream getAuthRequestHeader (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
    [
        let macAddressStorageKey = "wake-up-address"

        msgs

        let initialMacAddress = Browser.WebStorage.localStorage.getItem macAddressStorageKey
        AsyncRx.single (UpdateMacAddress initialMacAddress)

        let sendWakeUp macAddress =
            AsyncRx.defer (fun () ->
                AsyncRx.ofAsync (async {
                    let url = sprintf "/api/wake-up/%s" macAddress
                    let! authHeader = getAuthRequestHeader ()
                    let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                    let! response = Fetch.post(url, Encode.nil, Decode.succeed (), requestProperties) |> Async.AwaitPromise
                    return macAddress
                })
                |> AsyncRx.map Ok
                |> AsyncRx.catch (Error >> AsyncRx.single)
            )

        states
        |> AsyncRx.choose (function | (Some SendWakeUp, { MacAddress = (_, Some macAddress) }) -> Some (sendWakeUp macAddress) | _ -> None)
        |> AsyncRx.switchLatest
        |> AsyncRx.showSimpleSuccessToast (fun macAddress -> "Wake up", sprintf "Successfully sent wake up signal to \"%s\"" macAddress)
        |> AsyncRx.showSimpleErrorToast (fun e -> "Wake up failed", e.Message)
        |> AsyncRx.tapOnNext (function
            | Ok macAddress ->
                Browser.WebStorage.localStorage.setItem(macAddressStorageKey, macAddress)
            | Error _e -> ()
        )
        |> AsyncRx.map (Result.map ignore >> SendWakeUpResponse)
    ]
    |> AsyncRx.mergeSeq
