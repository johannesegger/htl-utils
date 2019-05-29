module Client

open Elmish
open Elmish.React
open Elmish.Streams
open Elmish.HMR // Must be last Elmish.* open declaration (see https://elmish.github.io/hmr/#Usage)
open Fable.Core.JsInterop
open Fable.React
open Fable.React.Props
open FSharp.Control
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

let update msg model =
    match msg with
    | ActivateTab tabItem ->
        { model with ActiveTab = tabItem }
    | AuthenticationMsg msg ->
        { model with Authentication = Authentication.update msg model.Authentication }
    | WakeUpMsg msg ->
        { model with WakeUp = WakeUp.update msg model.WakeUp }
    | ImportTeacherContactsMsg msg ->
        { model with ImportTeacherContacts = ImportTeacherContacts.update msg model.ImportTeacherContacts }
    | CreateStudentDirectoriesMsg msg ->
        { model with CreateStudentDirectories = CreateStudentDirectories.update msg model.CreateStudentDirectories }
    | CreateStudentGroupsMsg msg ->
        { model with CreateStudentGroups = CreateStudentGroups.update msg model.CreateStudentGroups }
    | InspectDirectoryMsg msg ->
        { model with InspectDirectory = InspectDirectory.update msg model.InspectDirectory }

let init() =
    let model =
        {
            ActiveTab = General
            Authentication = Authentication.init
            WakeUp = WakeUp.init
            ImportTeacherContacts = ImportTeacherContacts.init
            CreateStudentDirectories = CreateStudentDirectories.init
            CreateStudentGroups = CreateStudentGroups.init
            InspectDirectory = InspectDirectory.init
        }
    model

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

// let stream model msgs =
//     let authHeader = Authentication.tryGetAuthHeader model.Authentication
//     msgs
//     |> Stream.subStream Authentication.stream model.Authentication asAuthenticationMsg AuthenticationMsg "authentication"
//     |> Stream.subStream (CreateStudentDirectories.stream authHeader) model.CreateStudentDirectories asCreateStudentDirectoriesMsg CreateStudentDirectoriesMsg "createStudentDirectories"
//     |> Stream.subStream (ImportTeacherContacts.stream authHeader) model.ImportTeacherContacts asImportTeacherContactsMsg ImportTeacherContactsMsg "importTeacherContacts"
//     |> Stream.subStream (WakeUp.stream authHeader) model.WakeUp asWakeUpMsg WakeUpMsg "wakeUp"
//     // |> Stream.subStream CreateStudentGroups.stream model.Info asInfoMsg InfoMsg "info"
//     // |> Stream.subStream InspectDirectory.stream model.Info asInfoMsg InfoMsg "info"

let stream states msgs =
    let authHeader =
        states
        |> AsyncRx.map ((fun model -> model.Authentication) >> Authentication.tryGetAuthHeader)
        |> AsyncRx.distinctUntilChanged
    [
        msgs
        |> AsyncRx.choose (function | ActivateTab _ as x -> Some x | _ -> None)

        (
            states |> AsyncRx.map (fun m -> m.Authentication),
            msgs |> AsyncRx.choose (function AuthenticationMsg msg -> Some msg | _ -> None)
        )
        ||> Authentication.stream
        |> AsyncRx.map AuthenticationMsg

        (
            states |> AsyncRx.map (fun m -> m.WakeUp),
            msgs |> AsyncRx.choose (function WakeUpMsg msg -> Some msg | _ -> None)
        )
        ||> WakeUp.stream authHeader
        |> AsyncRx.map WakeUpMsg

        (
            states |> AsyncRx.map (fun m -> m.CreateStudentDirectories),
            msgs |> AsyncRx.choose (function CreateStudentDirectoriesMsg msg -> Some msg | _ -> None)
        )
        ||> CreateStudentDirectories.stream authHeader
        |> AsyncRx.map CreateStudentDirectoriesMsg

        (
            states |> AsyncRx.map (fun m -> m.CreateStudentGroups),
            msgs |> AsyncRx.choose (function CreateStudentGroupsMsg msg -> Some msg | _ -> None)
        )
        ||> CreateStudentGroups.stream authHeader
        |> AsyncRx.map CreateStudentGroupsMsg

        (
            states |> AsyncRx.map (fun m -> m.InspectDirectory),
            msgs |> AsyncRx.choose (function InspectDirectoryMsg msg -> Some msg | _ -> None)
        )
        ||> InspectDirectory.stream authHeader
        |> AsyncRx.map InspectDirectoryMsg
    ]
    |> AsyncRx.mergeSeq

#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

Program.mkSimple init update view
|> Smaerts.Program.withStream stream
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Toast.Program.withToast Toast.renderFulma
|> Program.withReactBatched "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
