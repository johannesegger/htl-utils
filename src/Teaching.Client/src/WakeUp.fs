module WakeUp

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
    MacAddress: string * string option
    IsEditing: bool
}

type Msg =
    | BeginEdit
    | UpdateMacAddress of string
    | EndEdit
    | SendWakeUp
    | SendWakeUpResponse of Result<unit, exn>

let init = {
    MacAddress = "", None
    IsEditing = false
}

let validateMacAddress (value: string) =
    if System.String.IsNullOrWhiteSpace value
    then None
    else Some value

let update msg model =
    match msg with
    | BeginEdit -> { model with IsEditing = true }
    | UpdateMacAddress value -> { model with MacAddress = (value, validateMacAddress value) }
    | EndEdit -> { model with IsEditing = false }
    | SendWakeUp -> model
    | SendWakeUpResponse (Ok ()) -> model
    | SendWakeUpResponse (Error _e) -> model

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
                if model.IsEditing
                then
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
                else
                    Tag.tag [ Tag.Size IsLarge ] [ str (model.MacAddress |> snd |> Option.defaultValue "<No MAC address specified>") ]
                    tagButton "is-warning" [ Button.OnClick (fun _ev -> dispatch BeginEdit) ] [
                        Icon.icon [] [ Fa.i [ Fa.Solid.PencilAlt ] [] ]
                    ]
                tagButton "is-success"
                    [
                        Button.Disabled (snd model.MacAddress |> Option.isNone || model.IsEditing)
                        Button.OnClick (fun _ev -> dispatch SendWakeUp)
                    ]
                    [
                        Icon.icon [] [ Fa.i [ Fa.Solid.PaperPlane ] [] ]
                    ]
            ]
        ]
    ]

let stream (authHeader: IAsyncObservable<HttpRequestHeaders option>) (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
    authHeader
    |> AsyncRx.choose id
    |> AsyncRx.flatMapLatest (fun authHeader ->
        [
            let macAddressStorageKey = "wake-up-address"

            msgs

            let initialMacAddress = Browser.WebStorage.localStorage.getItem macAddressStorageKey
            AsyncRx.single (UpdateMacAddress initialMacAddress)

            let sendWakeUp macAddress =
                AsyncRx.defer (fun () ->
                    AsyncRx.ofPromise (promise {
                        let url = sprintf "/api/wake-up/%s" macAddress
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        return!
                            Fetch.post(url, Encode.nil, Decode.succeed (), requestProperties)
                            |> Promise.map (fun () -> macAddress)
                    })
                    |> AsyncRx.map Ok
                    |> AsyncRx.catch (Error >> AsyncRx.single)
                )

            states
            |> AsyncRx.choose (function | (Some SendWakeUp, { MacAddress = (_, Some macAddress) }) -> Some (sendWakeUp macAddress) | _ -> None)
            |> AsyncRx.switchLatest
            |> AsyncRx.showSuccessToast (fun macAddress -> "Wake up", sprintf "Successfully sent wake up signal to \"%s\"" macAddress)
            |> AsyncRx.showErrorToast (fun e -> "Wake up failed", e.Message)
            |> AsyncRx.tapOnNext (function
                | Ok macAddress ->
                    Browser.WebStorage.localStorage.setItem(macAddressStorageKey, macAddress)
                | Error _e -> ()
            )
            |> AsyncRx.map (Result.map ignore >> SendWakeUpResponse)
        ]
        |> AsyncRx.mergeSeq
    )
