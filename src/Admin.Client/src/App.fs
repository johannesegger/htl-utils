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
                [ Heading.h2 [ Heading.Props [ Style [ FontVariant "small-caps" ] ] ]
                    [ str "Htl Mgmt" ] ]
              Navbar.End.div []
                [ Navbar.Item.div []
                    [ Authentication.view model.Authentication (AuthenticationMsg >> dispatch) ] ] ]
          yield pageHtml model.CurrentPage ]

let stream states msgs =
    let authHeader =
        states
        |> AsyncRx.map (snd >> (fun model -> model.Authentication) >> Authentication.tryGetAuthHeader)
        |> AsyncRx.distinctUntilChanged
    [
        (
            states
            |> AsyncRx.choose (fun (msg, model) ->
                match msg with
                | None
                | Some (AuthenticationMsg _) as msg -> Some (msg, model.Authentication)
                | Some _ -> None
            ),
            msgs |> AsyncRx.choose (function AuthenticationMsg msg -> Some msg | _ -> None)
        )
        ||> Authentication.stream
        |> AsyncRx.map AuthenticationMsg
    ]
    |> AsyncRx.mergeSeq


Program.mkSimple init update root
|> Program.withStream stream
|> Program.toNavigable (parseHash pageParser) urlUpdate
#if DEBUG
|> Program.withDebugger
#endif
|> Program.withReactBatched "elmish-app"
|> Program.run
