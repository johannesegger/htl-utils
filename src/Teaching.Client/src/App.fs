module App

open Browser
open Elmish
open Elmish.Debug
open Elmish.Navigation
open Elmish.React
open Elmish.UrlParser
open Elmish.HMR // Must be last Elmish.* open declaration (see https://elmish.github.io/hmr/#Usage)
open Fable.Core.JsInterop
open Fable.Elmish.Nile
open Fable.React
open Fable.React.Props
open FSharp.Control
open Fulma
open Pages
open Thoth.Elmish.Toast

importAll "../sass/main.sass"

type Msg =
    | AuthenticationMsg of Authentication.Msg
    | WakeUpMsg of WakeUp.Msg
    | AddAADTeacherContactsMsg of AddAADTeacherContacts.Msg
    | CreateStudentDirectoriesMsg of CreateStudentDirectories.Msg
    | CreateStudentGroupsMsg of CreateStudentGroups.Msg

type Model =
    {
        CurrentPage: Page
        Authentication: Authentication.Model
        WakeUp: WakeUp.Model
        AddAADTeacherContacts: AddAADTeacherContacts.Model
        CreateStudentDirectories: CreateStudentDirectories.Model
        CreateStudentGroups: CreateStudentGroups.Model
    }

let urlUpdate (result : Page option) model =
    match result with
    | None ->
        console.error("Error parsing url")
        model, Navigation.modifyUrl (toHash model.CurrentPage)
    | Some page ->
        { model with CurrentPage = page }, []

let init page =
    {
        CurrentPage = Option.defaultValue Home page
        Authentication = Authentication.init
        WakeUp = WakeUp.init
        AddAADTeacherContacts = AddAADTeacherContacts.init
        CreateStudentDirectories = CreateStudentDirectories.init
        CreateStudentGroups = CreateStudentGroups.init
    }

let update msg model =
    match msg with
    | AuthenticationMsg msg ->
        { model with Authentication = Authentication.update msg model.Authentication }
    | WakeUpMsg msg ->
        { model with WakeUp = WakeUp.update msg model.WakeUp }
    | AddAADTeacherContactsMsg msg ->
        { model with AddAADTeacherContacts = AddAADTeacherContacts.update msg model.AddAADTeacherContacts }
    | CreateStudentDirectoriesMsg msg ->
        { model with CreateStudentDirectories = CreateStudentDirectories.update msg model.CreateStudentDirectories }
    | CreateStudentGroupsMsg msg ->
        { model with CreateStudentGroups = CreateStudentGroups.update msg model.CreateStudentGroups }

let root model dispatch =
    let pageHtml = function
        | Home -> Home.view
        | WakeUp -> WakeUp.view model.WakeUp (WakeUpMsg >> dispatch)
        | AddAADTeacherContacts -> AddAADTeacherContacts.view model.AddAADTeacherContacts (AddAADTeacherContactsMsg >> dispatch)
        | CreateStudentDirectories -> CreateStudentDirectories.view model.CreateStudentDirectories (CreateStudentDirectoriesMsg >> dispatch)
        | CreateStudentGroups -> CreateStudentGroups.view model.CreateStudentGroups (CreateStudentGroupsMsg >> dispatch)

    div []
        [ yield Navbar.navbar [ Navbar.Color IsWarning ]
            [ Navbar.Item.div []
                [ h1 [ Class "title" ]
                    [ str "Htl utils" ] ]
              Navbar.End.div []
                [ Navbar.Item.div []
                    [ Authentication.view model.Authentication (AuthenticationMsg >> dispatch) ] ] ]
          yield div [ Style [ MarginTop "1em" ] ] [ pageHtml model.CurrentPage ] ]

let stream states msgs =
    let subStates chooseMsgFn subStateFn =
        states
        |> AsyncRx.choose (fun (msg, model) ->
            match msg with
            | None -> Some (None, subStateFn model)
            | Some (UserMsg msg) ->
                match chooseMsgFn msg with
                | Some msg -> Some (Some msg, subStateFn model)
                | None -> None
            | Some _ -> None
        )
    let authHeader =
        states
        |> AsyncRx.map (snd >> (fun model -> model.Authentication) >> Authentication.tryGetAuthHeader)
        |> AsyncRx.distinctUntilChanged
    let pageActivated filterPage =
        states
        |> AsyncRx.map (fun (msg, model) -> filterPage model.CurrentPage)
        |> AsyncRx.distinctUntilChanged
        |> AsyncRx.filter ((=) true)
        |> AsyncRx.map ignore
    let authHeaderAndPageActivated filterPage =
        authHeader
        |> AsyncRx.combineLatest (pageActivated filterPage)
        |> AsyncRx.map fst
    [
        (
            subStates (function AuthenticationMsg msg -> Some msg | _ -> None) (fun m -> m.Authentication),
            msgs |> AsyncRx.choose (function UserMsg (AuthenticationMsg msg) -> Some msg | _ -> None)
        )
        ||> Authentication.stream
        |> AsyncRx.map AuthenticationMsg

        (
            authHeaderAndPageActivated (function WakeUp -> true | _ -> false),
            subStates (function WakeUpMsg msg -> Some msg | _ -> None) (fun m -> m.WakeUp),
            msgs |> AsyncRx.choose (function UserMsg (WakeUpMsg msg) -> Some msg | _ -> None)
        )
        |||> WakeUp.stream
        |> AsyncRx.map WakeUpMsg

        (
            authHeaderAndPageActivated (function AddAADTeacherContacts -> true | _ -> false),
            subStates (function AddAADTeacherContactsMsg msg -> Some msg | _ -> None) (fun m -> m.AddAADTeacherContacts),
            msgs |> AsyncRx.choose (function UserMsg (AddAADTeacherContactsMsg msg) -> Some msg | _ -> None)
        )
        |||> AddAADTeacherContacts.stream
        |> AsyncRx.map AddAADTeacherContactsMsg

        (
            authHeaderAndPageActivated (function CreateStudentDirectories -> true | _ -> false),
            subStates (function CreateStudentDirectoriesMsg msg -> Some msg | _ -> None) (fun m -> m.CreateStudentDirectories),
            msgs |> AsyncRx.choose (function UserMsg (CreateStudentDirectoriesMsg msg) -> Some msg | _ -> None)
        )
        |||> CreateStudentDirectories.stream
        |> AsyncRx.map CreateStudentDirectoriesMsg

        (
            pageActivated (function CreateStudentGroups -> true | _ -> false),
            subStates (function CreateStudentGroupsMsg msg -> Some msg | _ -> None) (fun m -> m.CreateStudentGroups),
            msgs |> AsyncRx.choose (function UserMsg (CreateStudentGroupsMsg msg) -> Some msg | _ -> None)
        )
        |||> CreateStudentGroups.stream
        |> AsyncRx.map CreateStudentGroupsMsg
    ]
    |> AsyncRx.mergeSeq
    |> AsyncRx.map UserMsg

Program.mkSimple init update root
|> Program.toNavigable (parseHash pageParser) urlUpdate
|> Program.withStream stream
#if DEBUG
|> Program.withDebugger
|> Program.withConsoleTrace
#endif
|> Program.withToast Toast.renderFulma
|> Program.withReactBatched "elmish-app"
|> Program.run
