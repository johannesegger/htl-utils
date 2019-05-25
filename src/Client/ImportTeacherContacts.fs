module ImportTeacherContacts

open Elmish
open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open Fulma
open Thoth.Elmish
open Thoth.Fetch
open Thoth.Json

type Model = unit

type Msg =
    | Import
    | ImportResponse of Result<unit, exn>

let rec update authHeaderOptFn msg model =
    match msg with
    | Import ->
        match authHeaderOptFn with
        | Some getAuthHeader ->
            let makeRequestCmd =
                Cmd.OfPromise.either
                    (fun getAuthHeader -> promise {
                        let url = "/api/teachers/import-contacts"
                        let data = Encode.nil
                        let! authHeader = getAuthHeader ()
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        return! Fetch.post(url, data, requestProperties)
                    })
                    getAuthHeader
                    (ignore >> Ok >> ImportResponse)
                    (Error >> ImportResponse)
            let toastCmd =
                Toast.toast "Import teacher contacts" "Import started. This might take some time."
                |> Toast.info
            model, Cmd.batch [ makeRequestCmd; toastCmd ]
        | None ->
            let msg = exn "Please sign in using your Microsoft account." |> Error |> ImportResponse
            update authHeaderOptFn msg model
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
                    [ Icon.icon [] [ Fa.i [ Fa.Solid.IdCard ] [] ]
                      span [] [ str "Import teacher contacts" ] ] ] ] ]
