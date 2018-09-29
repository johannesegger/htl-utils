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

type Tab =
    | General
    | CreateStudentDirectories
    | CreateStudentGroups

type Model =
    { ActiveTab: Tab
      Authentication: Authentication.Model
      WakeUp: WakeUp.Model
      ImportTeacherContacts: ImportTeacherContacts.Model
      CreateStudentDirectories: CreateStudentDirectories.Model
      CreateStudentGroups: CreateStudentGroups.Model }

type Msg =
    | ActivateTab of Tab
    | AuthenticationMsg of Authentication.Msg
    | WakeUpMsg of WakeUp.Msg
    | ImportTeacherContactsMsg of ImportTeacherContacts.Msg
    | CreateStudentDirectoriesMsg of CreateStudentDirectories.Msg
    | CreateStudentGroupsMsg of CreateStudentGroups.Msg

let rec updateIfSignedIn auth (model, cmd) =
    match auth, model.Authentication with
    | Authentication.NotAuthenticated, Authentication.Authenticated _ ->
        let model', cmd' = update (CreateStudentDirectoriesMsg CreateStudentDirectories.Init) model
        model', Cmd.batch [ cmd; cmd' ]
    | _ -> model, cmd

and update msg model =
    let authHeaderOptFn = Authentication.authHeaderOptFn model.Authentication
    match msg with
    | ActivateTab tabItem ->
        let model' = { model with ActiveTab = tabItem }
        model', Cmd.none
    | AuthenticationMsg msg ->
        let subModel, subCmd = Authentication.update msg model.Authentication
        { model with Authentication = subModel }, Cmd.map AuthenticationMsg subCmd
    | WakeUpMsg msg ->
        let subModel, subCmd = WakeUp.update authHeaderOptFn msg model.WakeUp
        { model with WakeUp = subModel }, Cmd.map WakeUpMsg subCmd
    | ImportTeacherContactsMsg msg ->
        let subModel, subCmd = ImportTeacherContacts.update authHeaderOptFn msg model.ImportTeacherContacts
        { model with ImportTeacherContacts = subModel }, Cmd.map ImportTeacherContactsMsg subCmd
    | CreateStudentDirectoriesMsg msg ->
        let subModel, subCmd = CreateStudentDirectories.update authHeaderOptFn msg model.CreateStudentDirectories
        { model with CreateStudentDirectories = subModel }, Cmd.map CreateStudentDirectoriesMsg subCmd
    | CreateStudentGroupsMsg msg ->
        let subModel, subCmd = CreateStudentGroups.update msg model.CreateStudentGroups
        { model with CreateStudentGroups = subModel }, Cmd.map CreateStudentGroupsMsg subCmd
    |> updateIfSignedIn model.Authentication

let init() =
    let authModel, authCmd = Authentication.init()
    let wakeUpModel, wakeUpCmd = WakeUp.init()
    let importTeacherContactsModel, importTeacherContactsCmd = ImportTeacherContacts.init()
    let authHeaderOptFn = Authentication.authHeaderOptFn authModel
    let createStudentDirectoriesModel, createStudentDirectoriesCmd = CreateStudentDirectories.init authHeaderOptFn
    let createStudentGroupsModel, createStudentGroupsCmd = CreateStudentGroups.init
    let model =
        { ActiveTab = General
          Authentication = authModel
          WakeUp = wakeUpModel
          ImportTeacherContacts = importTeacherContactsModel
          CreateStudentDirectories = createStudentDirectoriesModel
          CreateStudentGroups = createStudentGroupsModel }
    let cmd =
        Cmd.batch
            [ Cmd.map AuthenticationMsg authCmd
              Cmd.map WakeUpMsg wakeUpCmd
              Cmd.map ImportTeacherContactsMsg importTeacherContactsCmd
              Cmd.map CreateStudentDirectoriesMsg createStudentDirectoriesCmd
              Cmd.map CreateStudentGroupsMsg createStudentGroupsCmd ]
    model, cmd

let view (model : Model) (dispatch : Msg -> unit) =
    let tabs =
        let tabItems =
            [ General, "General", [ WakeUp.view model.WakeUp (WakeUpMsg >> dispatch)
                                    ImportTeacherContacts.view model.ImportTeacherContacts (ImportTeacherContactsMsg >> dispatch) ]
              CreateStudentDirectories, "Create student directories", [ CreateStudentDirectories.view model.CreateStudentDirectories (CreateStudentDirectoriesMsg >> dispatch) ]
              CreateStudentGroups, "Create student groups", [ CreateStudentGroups.view model.CreateStudentGroups (CreateStudentGroupsMsg >> dispatch) ] ]
        [ yield Tabs.tabs []
            [ for (tabItem, tabName, _tabView) in tabItems ->
                Tabs.tab [ Tabs.Tab.IsActive (model.ActiveTab = tabItem) ]
                    [ a [ OnClick (fun _ev -> dispatch (ActivateTab tabItem)) ]
                        [ str tabName ] ] ]
          yield!
            tabItems
            |> List.choose (fun (tabItem, _, tabView) ->
                if model.ActiveTab = tabItem
                then Some tabView
                else None)
            |> List.collect id ]
    div []
        [ yield Navbar.navbar [ Navbar.Color IsWarning ]
            [ Navbar.Item.div []
                [ Heading.h2 [ Heading.Props [ Style [ FontVariant "small-caps" ] ] ]
                    [ str "Eggj utils" ] ]
              Navbar.End.div []
                [ Navbar.Item.div []
                    [ Authentication.view model.Authentication (AuthenticationMsg >> dispatch) ] ] ]
          yield! tabs ]

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
