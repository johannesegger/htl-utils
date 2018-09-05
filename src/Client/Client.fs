module Client

open Elmish
open Elmish.React
open Fable.Core
open Fable.Core.JsInterop
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.Import
open Fable.PowerPack
open Fable.PowerPack.Fetch
open Fulma
open Fulma.FontAwesome
open Thoth.Elmish

importAll "./Styles/main.sass"

type WakeUpCommandResponse =
    | Succeeded
    | Failed of string

type Model = unit

type Msg =
    | SendWakeUpCommand
    | SendWakeUpResponse of Result<unit, exn>

let init() =
    (), Cmd.none

[<PassGenerics>]
let private fetchAs<'a> url init =
    GlobalFetch.fetch(RequestInfo.Url url, requestProps init)
    |> Promise.bind (fun response ->
        if response.Ok then
            Promise.lift response
        else promise {
            let! text = response.text()
            return failwith text
        })
    |> Promise.bind (fun fetched -> fetched.text())
    |> Promise.map ofJson<'a>

let private toast title message =
    Toast.message message
    |> Toast.title title
    |> Toast.position Toast.TopRight
    |> Toast.noTimeout
    |> Toast.withCloseButton
    |> Toast.dismissOnClick

let update msg model =
    match msg with
    | SendWakeUpCommand ->
        let cmd =
            Cmd.ofPromise
                (fetchAs<unit> "/api/send-wakeup-command")
                []
                (Ok >> SendWakeUpResponse)
                (Error >> SendWakeUpResponse)
        model, cmd
    | SendWakeUpResponse (Error e) ->
        let cmd =
            toast "Wake up" e.Message
            |> Toast.error
        (), cmd
    | SendWakeUpResponse (Ok ()) ->
        let cmd =
            toast "Wake up" "Wake up signal successfully sent"
            |> Toast.success
        (), cmd

let view (model : Model) (dispatch : Msg -> unit) =
    div []
        [ Navbar.navbar [ Navbar.Color IsWarning ]
            [ Navbar.Item.div [ ]
                [ Heading.h2 [ Heading.Props [ Style [ FontVariant "small-caps" ] ] ]
                    [ str "Eggj utils" ] ] ]

          Container.container [ Container.Props [ Style [ MarginTop "1rem" ] ] ]
              [ Content.content [ ]
                  [ Button.list [ Button.List.IsCentered ]
                      [ Button.button
                            [ Button.IsLink
                              Button.OnClick (fun _evt -> dispatch SendWakeUpCommand)
                            ]
                            [ Icon.faIcon [ Icon.Size IsSmall ]
                                [ Fa.icon Fa.I.Bed ]
                              span [] [ str "Wake up PC-EGGJ" ]
                            ]
                      ]
                  ]
              ]
        ]

#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

Program.mkProgram init update view
#if DEBUG
|> Program.withConsoleTrace
|> Program.withHMR
#endif
|> Toast.Program.withToast Toast.renderFulma
|> Program.withReact "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
