module App

open Browser
open Elmish
open Elmish.Debug
open Elmish.Navigation
open Elmish.React
open Elmish.UrlParser
open Elmish.HMR // Must be last Elmish.* open declaration (see https://elmish.github.io/hmr/#Usage)
open Fable.Core
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
    | InspectDirectoryMsg of InspectDirectory.Msg
    | KnowNameMsg of KnowName.Msg

type Model =
    {
        CurrentPage: Page
        Authentication: Authentication.Model
        WakeUp: WakeUp.Model
        AddAADTeacherContacts: AddAADTeacherContacts.Model
        CreateStudentDirectories: CreateStudentDirectories.Model
        CreateStudentGroups: CreateStudentGroups.Model
        InspectDirectory: InspectDirectory.Model
        KnowName: KnowName.Model
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
        InspectDirectory = InspectDirectory.init
        KnowName = KnowName.init
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
    | InspectDirectoryMsg msg ->
        { model with InspectDirectory = InspectDirectory.update msg model.InspectDirectory }
    | KnowNameMsg msg ->
        { model with KnowName = KnowName.update msg model.KnowName }

let root model dispatch =
    let pageHtml = function
        | Home -> Home.view
        | WakeUp -> WakeUp.view model.WakeUp (WakeUpMsg >> dispatch)
        | AddAADTeacherContacts -> AddAADTeacherContacts.view model.AddAADTeacherContacts (AddAADTeacherContactsMsg >> dispatch)
        | CreateStudentDirectories -> CreateStudentDirectories.view model.CreateStudentDirectories (CreateStudentDirectoriesMsg >> dispatch)
        | CreateStudentGroups -> CreateStudentGroups.view model.CreateStudentGroups (CreateStudentGroupsMsg >> dispatch)
        | InspectDirectory -> InspectDirectory.view model.InspectDirectory (InspectDirectoryMsg >> dispatch)
        | KnowName -> KnowName.view model.KnowName (KnowNameMsg >> dispatch)

    div []
        [ yield Navbar.navbar [ Navbar.Color IsWarning ]
            [ Navbar.Item.div []
                [ h1 [ Class "title" ]
                    [ str "Htl utils" ] ]
              Navbar.End.div []
                [ Navbar.Item.div []
                    [ Authentication.view model.Authentication (AuthenticationMsg >> dispatch) ] ] ]
          yield div [ Style [ MarginTop "1em" ] ] [ pageHtml model.CurrentPage ] ]

let stream (states: IAsyncObservable<Navigable<Msg> option * Model>) (msgs: IAsyncObservable<Navigable<Msg>>) =
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

    let pageActivated (filterPage : Page -> bool) =
        states
        |> AsyncRx.map (fun (msg, model) -> filterPage model.CurrentPage)
        |> AsyncRx.distinctUntilChanged

    let loginObserver, loginObservable = AsyncRx.subject ()
    let login () = async {
        do! loginObserver.OnNextAsync ()
        return!
            states
            |> AsyncRx.filter (fst >> function | Some (UserMsg (AuthenticationMsg (Authentication.SignInResult _))) -> true | _ -> false)
            |> AsyncRx.flatMap (snd >> fun state ->
                Authentication.tryGetLoggedInUser state.Authentication
                |> Option.map (Authentication.getRequestHeader >> AsyncRx.ofAsync)
                |> Option.defaultValue (AsyncRx.fail (exn "Please sign in using your Microsoft account."))
            )
            |> AsyncRx.take 1
            |> AsyncRx.awaitLast
    }

    [
        (
            subStates (function AuthenticationMsg msg -> Some msg | _ -> None) (fun m -> m.Authentication),
            msgs |> AsyncRx.choose (function UserMsg (AuthenticationMsg msg) -> Some msg | _ -> None) |> AsyncRx.merge (loginObservable |> AsyncRx.map (fun () -> Authentication.SignIn))
        )
        ||> Authentication.stream
        |> AsyncRx.map AuthenticationMsg

        (
            login,
            subStates (function WakeUpMsg msg -> Some msg | _ -> None) (fun m -> m.WakeUp),
            msgs |> AsyncRx.choose (function UserMsg (WakeUpMsg msg) -> Some msg | _ -> None)
        )
        |||> WakeUp.stream
        |> AsyncRx.map WakeUpMsg

        (
            login,
            subStates (function AddAADTeacherContactsMsg msg -> Some msg | _ -> None) (fun m -> m.AddAADTeacherContacts),
            msgs |> AsyncRx.choose (function UserMsg (AddAADTeacherContactsMsg msg) -> Some msg | _ -> None)
        )
        |||> AddAADTeacherContacts.stream
        |> AsyncRx.map AddAADTeacherContactsMsg

        (
            (login, pageActivated ((=) CreateStudentDirectories)),
            subStates (function CreateStudentDirectoriesMsg msg -> Some msg | _ -> None) (fun m -> m.CreateStudentDirectories),
            msgs |> AsyncRx.choose (function UserMsg (CreateStudentDirectoriesMsg msg) -> Some msg | _ -> None)
        )
        |||> CreateStudentDirectories.stream
        |> AsyncRx.map CreateStudentDirectoriesMsg

        (
            (login, pageActivated ((=) CreateStudentGroups)),
            subStates (function CreateStudentGroupsMsg msg -> Some msg | _ -> None) (fun m -> m.CreateStudentGroups),
            msgs |> AsyncRx.choose (function UserMsg (CreateStudentGroupsMsg msg) -> Some msg | _ -> None)
        )
        |||> CreateStudentGroups.stream
        |> AsyncRx.map CreateStudentGroupsMsg

        (
            (login, pageActivated ((=) InspectDirectory)),
            subStates (function InspectDirectoryMsg msg -> Some msg | _ -> None) (fun m -> m.InspectDirectory),
            msgs |> AsyncRx.choose (function UserMsg (InspectDirectoryMsg msg) -> Some msg | _ -> None)
        )
        |||> InspectDirectory.stream
        |> AsyncRx.map InspectDirectoryMsg

        (
            (login, pageActivated ((=) KnowName)),
            subStates (function KnowNameMsg msg -> Some msg | _ -> None) (fun m -> m.KnowName),
            msgs |> AsyncRx.choose (function UserMsg (KnowNameMsg msg) -> Some msg | _ -> None)
        )
        |||> KnowName.stream
        |> AsyncRx.map KnowNameMsg
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
|> Program.withToast Toast.renderToastWithFulma
|> Program.withReactBatched "elmish-app"
|> Program.run
