module ImportTeacherContacts

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
    | Import
    | ImportResponse of Result<unit, exn>

let update authHeaderOptFn msg model =
    match msg with
    | Import ->
        let cmd =
            match authHeaderOptFn with
            | Some getAuthHeader ->
                Cmd.ofPromise
                    (getAuthHeader >> Promise.bind (List.singleton >> requestHeaders >> List.singleton >> postRecord "/api/teachers/import-contacts" ()))
                    ()
                    (ignore >> Ok >> ImportResponse)
                    (Error >> ImportResponse)
            | None -> Cmd.none
        model, cmd
    | ImportResponse (Error e) ->
        let cmd =
            Toast.toast "Import teacher contacts failed" e.Message
            |> Toast.error
        model, cmd
    | ImportResponse (Ok ()) ->
        let cmd =
            Toast.toast "Import teacher contacts" "Import successful"
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
                      Button.OnClick (fun _evt -> dispatch Import) ]
                    [ Icon.faIcon [ Icon.Size IsSmall ]
                        [ Fa.icon Fa.I.Vcard ]
                      span [] [ str "Import teacher contacts" ] ] ] ] ]
