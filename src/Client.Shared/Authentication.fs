module Authentication

open Fable.Core
open Fable.Core.JsInterop
open Fable.FontAwesome
open Fable.React
open Fable.Reaction
open FSharp.Control
open Fulma

type User = {
    Name: string
    AccessToken: string
}

type AuthState =
    | NotAuthenticated
    | Authenticated of User

type Model =
    | Loading
    | Loaded of AuthState

type Msg =
    | InitialState of AuthState
    | SignIn
    | SignInResult of Result<User, exn>
    | SignOut
    | SignOutResult of Result<unit, exn>

let private appId = "9fb9b79b-6e66-4007-a94f-571d7e3b68c5"
let private userAgentApplication =
    let options =
        let cacheOptions = createEmpty<Msal.CacheOptions>
        cacheOptions.cacheLocation <- Some Msal.CacheLocation.LocalStorage

        let authOptions = createEmpty<Msal.AuthOptions>
        authOptions.authority <- Some "https://login.microsoftonline.com/htlvb.at/"
        authOptions.clientId <- appId

        let o = createEmpty<Msal.Configuration>
        o.cache <- Some cacheOptions
        o.auth <- authOptions
        o
    Msal.UserAgentApplication.Create(options)

[<Emit("$0.name === \"InteractionRequiredAuthError\"")>]
let private isInteractionRequiredAuthError (_ : exn) : bool = jsNative

let private authenticateUser = async {
    match userAgentApplication.getAccount() |> Option.ofObj with
    | Some account ->
        Browser.Dom.console.log("[Auth] Account found. Acquiring token.")
        let! authResponse = async {
            let authParams = createEmpty<Msal.AuthenticationParameters>
            authParams.scopes <- Some !![| appId |]
            try
                return! userAgentApplication.acquireTokenSilent authParams |> Async.AwaitPromise
            with e when isInteractionRequiredAuthError e ->
                Browser.Dom.console.log("[Auth] Acquiring token silently failed. Showing popup.")
                return! userAgentApplication.acquireTokenPopup authParams |> Async.AwaitPromise
        }
        Browser.Dom.console.log("[Auth] Auth response", authResponse)
        return { Name = account.name; AccessToken = authResponse.idToken.rawIdToken }
    | None ->
        Browser.Dom.console.log("[Auth] No account found. Logging in.")
        let authParams = createEmpty<Msal.AuthenticationParameters>
        let! authResponse = userAgentApplication.loginPopup authParams |> Async.AwaitPromise
        Browser.Dom.console.log("[Auth] Auth response", authResponse)
        return { Name = authResponse.account.name; AccessToken = authResponse.accessToken }
}

let tryGetLoggedInUser model =
    match model with
    | Loading
    | Loaded NotAuthenticated -> None
    | Loaded (Authenticated user) -> Some user

let getRequestHeader user = async {
    return sprintf "Bearer %s" user.AccessToken |> Fetch.Types.Authorization
}

let rec update msg model =
    match msg with
    | InitialState model -> Loaded model
    | SignIn -> model
    | SignInResult (Ok user) -> Loaded (Authenticated user)
    | SignInResult (Error _e) -> Loaded NotAuthenticated
    | SignOut -> model
    | SignOutResult  (Ok ()) -> Loaded NotAuthenticated
    | SignOutResult  (Error _e) -> Loaded NotAuthenticated

let init = Loading

let view model dispatch =
    match model with
    | Loading ->
        Button.button [ Button.IsLoading true ] []
    | Loaded NotAuthenticated ->
        Button.button
            [ Button.OnClick (fun _e -> dispatch SignIn) ]
            [
                Icon.icon [] [ Fa.i [ Fa.Brand.Windows ] [] ]
                span [] [ str "Sign in" ]
            ]
    | Loaded (Authenticated user) ->
        Button.button
            [ Button.OnClick (fun _e -> dispatch SignOut) ]
            [
                Icon.icon [] [ Fa.i [ Fa.Brand.Windows ] [] ]
                span [] [ str (sprintf "%s | Sign out" user.Name) ]
            ]

let stream states msgs =
    [
        yield
            states
            |> AsyncRx.map snd
            |> AsyncRx.distinctUntilChanged
            |> AsyncRx.choose (function
                | Loading ->
                    match userAgentApplication.getAccount() |> Option.ofObj with
                    | Some user ->
                        Authenticated { Name = user.name; AccessToken = "" } |> Some
                    | None ->
                        Some NotAuthenticated
                | _ -> None
            )
            |> AsyncRx.map InitialState

        let login =
            AsyncRx.defer (fun () -> AsyncRx.ofAsync authenticateUser)
            |> AsyncRx.map Ok
            |> AsyncRx.catch (Error >> AsyncRx.single)
        yield
            msgs
            |> AsyncRx.choose (function | SignIn -> Some login | _ -> None)
            |> AsyncRx.switchLatest
            |> AsyncRx.showSimpleErrorToast (fun e -> "Login failed", e.Message)
            |> AsyncRx.map SignInResult

        let logout =
            AsyncRx.defer (fun () -> AsyncRx.single (userAgentApplication.logout()))
            |> AsyncRx.map (Ok >> SignOutResult)
            |> AsyncRx.catch (Error >> SignOutResult >> AsyncRx.single)
        yield
            msgs
            |> AsyncRx.choose (function | SignOut -> Some logout | _ -> None)
            |> AsyncRx.switchLatest
    ]
    |> AsyncRx.mergeSeq
