module Client

open Elmish
open Elmish.React

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch

open Fulma

type WakeUpCommandResponse =
    | Succeeded
    | Failed of string

type Model = {
    WakeUpCommandResponse: WakeUpCommandResponse option
}

type Msg =
    | SendWakeUpCommand
    | SendWakeUpResponse of Result<unit, exn>

let init() =
    { WakeUpCommandResponse = None }, Cmd.none

let update msg model =
    match msg with
    | SendWakeUpCommand ->
        model,
        Cmd.ofPromise
            (fetchAs<unit> "/api/send-wakeup-command")
            []
            (Ok >> SendWakeUpResponse)
            (Error >> SendWakeUpResponse)
    | SendWakeUpResponse (Error e) ->
        { model with
            WakeUpCommandResponse = Failed e.Message |> Some },
        Cmd.none
    | SendWakeUpResponse (Ok ()) ->
        { model with
            WakeUpCommandResponse = Some Succeeded },
        Cmd.none

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
                            [ str "Wake up PC-EGGJ" ]
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
|> Program.withReact "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
