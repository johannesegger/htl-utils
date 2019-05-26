module Client

open Elmish
open Elmish.React
open Elmish.HMR // Must be last Elmish.* open declaration (see https://elmish.github.io/hmr/#Usage)
open Fable.Core.JsInterop
open Fable.React
open Fable.React.Props
open Fulma
open Thoth.Elmish
open Shared

importAll "./styles/main.sass"

type Tab =
    | General
    | CreateStudentDirectories
    | CreateStudentGroups
    | InspectDirectory

type Model =
    {
        ActiveTab: Tab
        Authentication: Authentication.Model
        WakeUp: WakeUp.Model
        ImportTeacherContacts: ImportTeacherContacts.Model
        CreateStudentDirectories: CreateStudentDirectories.Model
        CreateStudentGroups: CreateStudentGroups.Model
        InspectDirectory: InspectDirectory.Model
    }

type Msg =
    | ActivateTab of Tab
    | AuthenticationMsg of Authentication.Msg
    | WakeUpMsg of WakeUp.Msg
    | ImportTeacherContactsMsg of ImportTeacherContacts.Msg
    | CreateStudentDirectoriesMsg of CreateStudentDirectories.Msg
    | CreateStudentGroupsMsg of CreateStudentGroups.Msg
    | InspectDirectoryMsg of InspectDirectory.Msg

let rec updateIfSignedIn auth (model, cmd) =
    match auth, model.Authentication with
    | Authentication.NotAuthenticated, Authentication.Authenticated _ ->
        let model', cmd' = update (CreateStudentDirectoriesMsg CreateStudentDirectories.Init) model
        let model'', cmd'' = update (InspectDirectoryMsg InspectDirectory.Init) model'
        model'', Cmd.batch [ cmd; cmd'; cmd'' ]
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
    | InspectDirectoryMsg msg ->
        let subModel, subCmd = InspectDirectory.update authHeaderOptFn msg model.InspectDirectory
        { model with InspectDirectory = subModel }, Cmd.map InspectDirectoryMsg subCmd
    |> updateIfSignedIn model.Authentication

let init() =
    let authModel, authCmd = Authentication.init()
    let wakeUpModel, wakeUpCmd = WakeUp.init()
    let importTeacherContactsModel, importTeacherContactsCmd = ImportTeacherContacts.init()
    let authHeaderOptFn = Authentication.authHeaderOptFn authModel
    let createStudentDirectoriesModel, createStudentDirectoriesCmd = CreateStudentDirectories.init authHeaderOptFn
    let createStudentGroupsModel, createStudentGroupsCmd = CreateStudentGroups.init
    let inspectDirectoryModel, inspectDirectoryCmd = InspectDirectory.init authHeaderOptFn
    let model =
        {
            ActiveTab = General
            Authentication = authModel
            WakeUp = wakeUpModel
            ImportTeacherContacts = importTeacherContactsModel
            CreateStudentDirectories = createStudentDirectoriesModel
            CreateStudentGroups = createStudentGroupsModel
            InspectDirectory = inspectDirectoryModel
        }
    let cmd =
        Cmd.batch
            [
                Cmd.map AuthenticationMsg authCmd
                Cmd.map WakeUpMsg wakeUpCmd
                Cmd.map ImportTeacherContactsMsg importTeacherContactsCmd
                Cmd.map CreateStudentDirectoriesMsg createStudentDirectoriesCmd
                Cmd.map CreateStudentGroupsMsg createStudentGroupsCmd
                Cmd.map InspectDirectoryMsg inspectDirectoryCmd
            ]
    model, cmd

let view (model : Model) (dispatch : Msg -> unit) =
    let tabs =
        let tabItems =
            [
                General, "General", [ WakeUp.view model.WakeUp (WakeUpMsg >> dispatch)
                                      ImportTeacherContacts.view model.ImportTeacherContacts (ImportTeacherContactsMsg >> dispatch) ]
                CreateStudentDirectories, "Create student directories", [ CreateStudentDirectories.view model.CreateStudentDirectories (CreateStudentDirectoriesMsg >> dispatch) ]
                CreateStudentGroups, "Create student groups", [ CreateStudentGroups.view model.CreateStudentGroups (CreateStudentGroupsMsg >> dispatch) ]
                InspectDirectory, "Inspect directory", [ InspectDirectory.view model.InspectDirectory (InspectDirectoryMsg >> dispatch) ]
            ]
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
                    [ str "Htl utils" ] ]
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
#endif
|> Toast.Program.withToast Toast.renderFulma
|> Program.withReactBatched "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
