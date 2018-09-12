module Client

open Elmish
open Elmish.React
open Fable.Core.JsInterop
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fulma
open Fulma.Extensions
open Thoth.Elmish
open Shared

importAll "./Styles/main.sass"

type Model =
    { WakeUp: WakeUp.Model
      CreateStudentDirectories: CreateStudentDirectories.Model }

type Msg =
    | WakeUpMsg of WakeUp.Msg
    | CreateStudentDirectoriesMsg of CreateStudentDirectories.Msg

let update msg model =
    match msg with
    | WakeUpMsg msg ->
        let subModel, subCmd = WakeUp.update msg model.WakeUp
        { model with WakeUp = subModel }, Cmd.map WakeUpMsg subCmd
    | CreateStudentDirectoriesMsg msg ->
        let subModel, subCmd = CreateStudentDirectories.update msg model.CreateStudentDirectories
        { model with CreateStudentDirectories = subModel }, Cmd.map CreateStudentDirectoriesMsg subCmd

let init() =
    let wakeUpModel, wakeUpCmd = WakeUp.init()
    let createStudentDirectoriesModel, createStudentDirectoriesCmd = CreateStudentDirectories.init()
    let model =
        { WakeUp = wakeUpModel
          CreateStudentDirectories = createStudentDirectoriesModel }

    let cmd' =
        Cmd.batch
            [ Cmd.map WakeUpMsg wakeUpCmd
              Cmd.map CreateStudentDirectoriesMsg createStudentDirectoriesCmd ]
    model, cmd'
            

let view (model : Model) (dispatch : Msg -> unit) =
    div []
        [ Navbar.navbar [ Navbar.Color IsWarning ]
            [ Navbar.Item.div [ ]
                [ Heading.h2 [ Heading.Props [ Style [ FontVariant "small-caps" ] ] ]
                    [ str "Eggj utils" ] ] ]
          
          WakeUp.view model.WakeUp (WakeUpMsg >> dispatch)
          Divider.divider [ Divider.Label "Create student directories" ]
          CreateStudentDirectories.view model.CreateStudentDirectories (CreateStudentDirectoriesMsg >> dispatch)
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
