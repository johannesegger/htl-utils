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
    { Authentication: Authentication.Model
      WakeUp: WakeUp.Model
      CreateStudentDirectories: CreateStudentDirectories.Model }

type Msg =
    | AuthenticationMsg of Authentication.Msg
    | WakeUpMsg of WakeUp.Msg
    | CreateStudentDirectoriesMsg of CreateStudentDirectories.Msg

let rec updateIfSignedIn auth (model, cmd) =
    match auth, model.Authentication with
    | Authentication.NotAuthenticated, Authentication.Authenticated _ ->
        let model', cmd' = update (CreateStudentDirectoriesMsg (CreateStudentDirectories.LoadChildDirectories [])) model
        let model'', cmd'' = update (CreateStudentDirectoriesMsg CreateStudentDirectories.LoadClassList) model'
        model'', Cmd.batch [ cmd; cmd'; cmd'' ]
    | _ -> model, cmd

and update msg model =
    let authHeaderOptFn = Authentication.authHeaderOptFn model.Authentication
    match msg with
    | AuthenticationMsg msg ->
        let subModel, subCmd = Authentication.update msg model.Authentication
        { model with Authentication = subModel }, Cmd.map AuthenticationMsg subCmd
    | WakeUpMsg msg ->
        let subModel, subCmd = WakeUp.update authHeaderOptFn msg model.WakeUp
        { model with WakeUp = subModel }, Cmd.map WakeUpMsg subCmd
    | CreateStudentDirectoriesMsg msg ->
        let subModel, subCmd = CreateStudentDirectories.update authHeaderOptFn msg model.CreateStudentDirectories
        { model with CreateStudentDirectories = subModel }, Cmd.map CreateStudentDirectoriesMsg subCmd
    |> updateIfSignedIn model.Authentication

let init() =
    let authModel, authCmd = Authentication.init()
    let wakeUpModel, wakeUpCmd = WakeUp.init()
    let authHeaderOptFn = Authentication.authHeaderOptFn authModel
    let createStudentDirectoriesModel, createStudentDirectoriesCmd = CreateStudentDirectories.init authHeaderOptFn
    let model =
        { Authentication = authModel
          WakeUp = wakeUpModel
          CreateStudentDirectories = createStudentDirectoriesModel }
    let cmd =
        Cmd.batch
            [ Cmd.map AuthenticationMsg authCmd
              Cmd.map WakeUpMsg wakeUpCmd
              Cmd.map CreateStudentDirectoriesMsg createStudentDirectoriesCmd ]
    model, cmd

let view (model : Model) (dispatch : Msg -> unit) =
    div []
        [ Navbar.navbar [ Navbar.Color IsWarning ]
            [ Navbar.Item.div []
                [ Heading.h2 [ Heading.Props [ Style [ FontVariant "small-caps" ] ] ]
                    [ str "Eggj utils" ] ]
              Navbar.End.div []
                [ Navbar.Item.div []
                    [ Authentication.view model.Authentication (AuthenticationMsg >> dispatch) ] ] ]
          
          WakeUp.view model.WakeUp (WakeUpMsg >> dispatch)
          Divider.divider [ Divider.Label "Create student directories" ]
          CreateStudentDirectories.view model.CreateStudentDirectories (CreateStudentDirectoriesMsg >> dispatch) ]

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
