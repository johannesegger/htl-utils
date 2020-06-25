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
    | SyncAADGroupsMsg of SyncAADGroups.Msg

type Model =
    {
        CurrentPage: Page
        Authentication: Authentication.Model
        SyncAADGroups: SyncAADGroups.Model
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
        SyncAADGroups = SyncAADGroups.init
    }

let update msg model =
    match msg with
    | AuthenticationMsg msg ->
        { model with Authentication = Authentication.update msg model.Authentication }
    | SyncAADGroupsMsg msg ->
        { model with SyncAADGroups = SyncAADGroups.update msg model.SyncAADGroups }

let root model dispatch =
    let pageHtml = function
        | Home -> Home.view
        | SyncAADGroups -> SyncAADGroups.view model.SyncAADGroups (SyncAADGroupsMsg >> dispatch)

    div []
        [ yield Navbar.navbar [ Navbar.Color IsDanger ]
            [ Navbar.Item.div []
                [ Heading.h2 []
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
        (
            subStates (function AuthenticationMsg msg -> Some msg | _ -> None) (fun m -> m.Authentication),
            msgs |> AsyncRx.choose (function UserMsg (AuthenticationMsg msg) -> Some msg | _ -> None) |> AsyncRx.merge (loginObservable |> AsyncRx.map (fun () -> Authentication.SignIn))
        )
        ||> Authentication.stream
        |> AsyncRx.map AuthenticationMsg

        (
            (login, pageActivated ((=) SyncAADGroups)),
            subStates (function SyncAADGroupsMsg msg -> Some msg | _ -> None) (fun m -> m.SyncAADGroups),
            msgs |> AsyncRx.choose (function UserMsg (SyncAADGroupsMsg msg) -> Some msg | _ -> None)
        )
        |||> SyncAADGroups.stream
        |> AsyncRx.map SyncAADGroupsMsg
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
