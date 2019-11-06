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

type Model =
    {
        CurrentPage: Page
        Authentication: Authentication.Model
        WakeUp: WakeUp.Model
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
    }

let update msg model =
    match msg with
    | AuthenticationMsg msg ->
        { model with Authentication = Authentication.update msg model.Authentication }
    | WakeUpMsg msg ->
        { model with WakeUp = WakeUp.update msg model.WakeUp }

let root model dispatch =
    let pageHtml = function
        | Home -> Home.view
        | WakeUp -> WakeUp.view model.WakeUp (WakeUpMsg >> dispatch)

    div []
        [ yield Navbar.navbar [ Navbar.Color IsWarning ]
            [ Navbar.Item.div []
                [ Heading.h2 [ Heading.Props [ Style [ FontVariant "small-caps" ] ] ]
                    [ str "Htl Utils" ] ]
              Navbar.End.div []
                [ Navbar.Item.div []
                    [ Authentication.view model.Authentication (AuthenticationMsg >> dispatch) ] ] ]
          yield pageHtml model.CurrentPage ]

let stream states msgs =
    let authHeader =
        states
        |> AsyncRx.map (snd >> (fun model -> model.Authentication) >> Authentication.tryGetAuthHeader)
        |> AsyncRx.distinctUntilChanged
    let pageActivated pageFn =
        states
        |> AsyncRx.map (fun (msg, model) ->
            pageFn model.CurrentPage
        )
        |> AsyncRx.distinctUntilChanged
        |> AsyncRx.filter ((=) true)
        |> AsyncRx.map ignore
    [
        (
            states
            |> AsyncRx.choose (fun (msg, model) ->
                match msg with
                | None
                | Some (UserMsg (AuthenticationMsg _)) as msg -> Some (msg, model.Authentication)
                | Some _ -> None
            ),
            msgs |> AsyncRx.choose (function UserMsg (AuthenticationMsg msg) -> Some msg | _ -> None)
        )
        ||> Authentication.stream
        |> AsyncRx.map AuthenticationMsg

        (
            authHeader
            |> AsyncRx.combineLatest (pageActivated ((=) WakeUp))
            |> AsyncRx.map fst,
            states
            |> AsyncRx.choose (fun (msg, model) ->
                match msg with
                | None -> Some (None, model.WakeUp)
                | Some (UserMsg (WakeUpMsg msg)) -> Some (Some msg, model.WakeUp)
                | Some _ -> None
            ),
            msgs |> AsyncRx.choose (function UserMsg (WakeUpMsg msg) -> Some msg | _ -> None)
        )
        |||> WakeUp.stream
        |> AsyncRx.map WakeUpMsg
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
