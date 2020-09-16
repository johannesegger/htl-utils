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
    | IncrementADClassGroupsMsg of IncrementADClassGroups.Msg
    | SyncADMsg of SyncAD.Msg
    | ModifyADMsg of ModifyAD.Msg
    | IncrementAADClassGroupsMsg of IncrementAADClassGroups.Msg
    | SyncAADGroupsMsg of SyncAADGroups.Msg
    | ListConsultationHoursMsg of ListConsultationHours.Msg
    | ShowComputerInfoMsg of ShowComputerInfo.Msg

type Model =
    {
        CurrentPage: Page
        Authentication: Authentication.Model
        IncrementADClassGroups: IncrementADClassGroups.Model
        SyncAD: SyncAD.Model
        ModifyAD: ModifyAD.Model
        IncrementAADClassGroups: IncrementAADClassGroups.Model
        SyncAADGroups: SyncAADGroups.Model
        ListConsultationHours: ListConsultationHours.Model
        ShowComputerInfo: ShowComputerInfo.Model
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
        IncrementADClassGroups = IncrementADClassGroups.init
        SyncAD = SyncAD.init
        ModifyAD = ModifyAD.init
        IncrementAADClassGroups = IncrementAADClassGroups.init
        SyncAADGroups = SyncAADGroups.init
        ListConsultationHours = ListConsultationHours.init
        ShowComputerInfo = ShowComputerInfo.init
    }

let update msg model =
    match msg with
    | AuthenticationMsg msg ->
        { model with Authentication = Authentication.update msg model.Authentication }
    | IncrementADClassGroupsMsg msg ->
        { model with IncrementADClassGroups = IncrementADClassGroups.update msg model.IncrementADClassGroups }
    | SyncADMsg msg ->
        { model with SyncAD = SyncAD.update msg model.SyncAD }
    | ModifyADMsg msg ->
        { model with ModifyAD = ModifyAD.update msg model.ModifyAD }
    | IncrementAADClassGroupsMsg msg ->
        { model with IncrementAADClassGroups = IncrementAADClassGroups.update msg model.IncrementAADClassGroups }
    | SyncAADGroupsMsg msg ->
        { model with SyncAADGroups = SyncAADGroups.update msg model.SyncAADGroups }
    | ListConsultationHoursMsg msg ->
        { model with ListConsultationHours = ListConsultationHours.update msg model.ListConsultationHours }
    | ShowComputerInfoMsg msg ->
        { model with ShowComputerInfo = ShowComputerInfo.update msg model.ShowComputerInfo }

let root model dispatch =
    let pageHtml = function
        | Home -> Home.view
        | IncrementADClassGroups -> IncrementADClassGroups.view model.IncrementADClassGroups (IncrementADClassGroupsMsg >> dispatch)
        | SyncAD -> SyncAD.view model.SyncAD (SyncADMsg >> dispatch)
        | ModifyAD -> ModifyAD.view model.ModifyAD (ModifyADMsg >> dispatch)
        | IncrementAADClassGroups -> IncrementAADClassGroups.view model.IncrementAADClassGroups (IncrementAADClassGroupsMsg >> dispatch)
        | SyncAADGroups -> SyncAADGroups.view model.SyncAADGroups (SyncAADGroupsMsg >> dispatch)
        | ListConsultationHours -> ListConsultationHours.view model.ListConsultationHours (ListConsultationHoursMsg >> dispatch)
        | ShowComputerInfo -> ShowComputerInfo.view model.ShowComputerInfo (ShowComputerInfoMsg >> dispatch)

    div []
        [ yield Navbar.navbar [ Navbar.Color IsDanger ]
            [ Navbar.Item.div []
                [ h1 [ Class "title" ]
                    [ str "Htl Mgmt" ] ]
              Navbar.End.div []
                [ Navbar.Item.div []
                    [ Authentication.view model.Authentication (AuthenticationMsg >> dispatch) ] ] ]
          yield pageHtml model.CurrentPage ]

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
        Authentication.stream
            (subStates (function AuthenticationMsg msg -> Some msg | _ -> None) (fun m -> m.Authentication))
            (msgs
                |> AsyncRx.choose (function UserMsg (AuthenticationMsg msg) -> Some msg | _ -> None)
                |> AsyncRx.merge (loginObservable |> AsyncRx.map (fun () -> Authentication.SignIn))
            )
        |> AsyncRx.map AuthenticationMsg

        IncrementADClassGroups.stream
            login
            (pageActivated ((=) IncrementADClassGroups))
            (subStates (function IncrementADClassGroupsMsg msg -> Some msg | _ -> None) (fun m -> m.IncrementADClassGroups))
            (msgs |> AsyncRx.choose (function UserMsg (IncrementADClassGroupsMsg msg) -> Some msg | _ -> None))
        |> AsyncRx.map IncrementADClassGroupsMsg

        SyncAD.stream
            login
            (pageActivated ((=) SyncAD))
            (subStates (function SyncADMsg msg -> Some msg | _ -> None) (fun m -> m.SyncAD))
            (msgs |> AsyncRx.choose (function UserMsg (SyncADMsg msg) -> Some msg | _ -> None))
        |> AsyncRx.map SyncADMsg

        ModifyAD.stream
            login
            (pageActivated ((=) ModifyAD))
            (subStates (function ModifyADMsg msg -> Some msg | _ -> None) (fun m -> m.ModifyAD))
            (msgs |> AsyncRx.choose (function UserMsg (ModifyADMsg msg) -> Some msg | _ -> None))
        |> AsyncRx.map ModifyADMsg

        IncrementAADClassGroups.stream
            login
            (pageActivated ((=) IncrementAADClassGroups))
            (subStates (function IncrementAADClassGroupsMsg msg -> Some msg | _ -> None) (fun m -> m.IncrementAADClassGroups))
            (msgs |> AsyncRx.choose (function UserMsg (IncrementAADClassGroupsMsg msg) -> Some msg | _ -> None))
        |> AsyncRx.map IncrementAADClassGroupsMsg

        SyncAADGroups.stream
            login
            (pageActivated ((=) SyncAADGroups))
            (subStates (function SyncAADGroupsMsg msg -> Some msg | _ -> None) (fun m -> m.SyncAADGroups))
            (msgs |> AsyncRx.choose (function UserMsg (SyncAADGroupsMsg msg) -> Some msg | _ -> None))
        |> AsyncRx.map SyncAADGroupsMsg

        ListConsultationHours.stream
            (pageActivated ((=) ListConsultationHours))
            (subStates (function ListConsultationHoursMsg msg -> Some msg | _ -> None) (fun m -> m.ListConsultationHours))
            (msgs |> AsyncRx.choose (function UserMsg (ListConsultationHoursMsg msg) -> Some msg | _ -> None))
        |> AsyncRx.map ListConsultationHoursMsg

        ShowComputerInfo.stream
            (pageActivated ((=) ShowComputerInfo))
            (subStates (function ShowComputerInfoMsg msg -> Some msg | _ -> None) (fun m -> m.ShowComputerInfo))
            (msgs |> AsyncRx.choose (function UserMsg (ShowComputerInfoMsg msg) -> Some msg | _ -> None))
        |> AsyncRx.map ShowComputerInfoMsg
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
