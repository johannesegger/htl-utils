module Authentication

open Fable.Core
open Fable.Core.JsInterop
open Fable.FontAwesome
open Fable.React
open Fable.Reaction
open FSharp.Control
open Fulma
open MsalBrowser
open MsalCommon

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

[<Import("PublicClientApplication", "@azure/msal-browser")>]
let publicClientApplication: PublicClientApplicationStatic =
    jsNative

let private appId = "fb763808-3d92-4310-896c-fc03fbd854b8"
let private clientApplication =
    let options =
        let cacheOptions = createEmpty<CacheOptions>
        cacheOptions.cacheLocation <- Some (U2.Case1 BrowserCacheLocation.LocalStorage)

        let authOptions = createEmpty<BrowserAuthOptions>
        authOptions.authority <- Some "https://login.microsoftonline.com/htlvb.at/"
        authOptions.clientId <- appId

        let o = createEmpty<Configuration>
        o.cache <- Some cacheOptions
        o.auth <- authOptions
        o
    publicClientApplication.Create(options)

let private authenticateUser = async {
    let tokenRequest = createObj [
        "scopes" ==> [| "api://235fe3a7-8dbd-426c-b7b1-3d64cb37724b/user_impersonation"; "Contacts.ReadWrite"; "Calendars.ReadWrite" |]
    ]
    let! loginResponse = async {
        match clientApplication.getActiveAccount() with
        | Some accountInfo ->
            match! Async.Catch (clientApplication.acquireTokenSilent !!tokenRequest |> Async.AwaitPromise) with
            | Choice1Of2 v ->
                return v
            | Choice2Of2 (:? InteractionRequiredAuthError as e) ->
                return! clientApplication.acquireTokenPopup !!tokenRequest |> Async.AwaitPromise
            | Choice2Of2 e ->
                return raise e
        | None ->
            let! authResult = clientApplication.loginPopup !!tokenRequest |> Async.AwaitPromise
            clientApplication.setActiveAccount(authResult.account)
            return! clientApplication.acquireTokenSilent !!tokenRequest |> Async.AwaitPromise
    }
    return { Name = loginResponse.account |> Option.bind (fun v -> v.name) |> Option.defaultValue ""; AccessToken = loginResponse.accessToken }
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
                    match clientApplication.getActiveAccount() with
                    | Some user ->
                        Authenticated { Name = user.name |> Option.defaultValue ""; AccessToken = "" } |> Some
                    | None ->
                        Some NotAuthenticated
                | _ -> None
            )
            |> AsyncRx.map InitialState

        let login =
            AsyncRx.defer (fun () -> AsyncRx.ofAsync authenticateUser)
            |> AsyncRx.map Ok
            |> AsyncRx.catch (Result.Error >> AsyncRx.single)
        yield
            msgs
            |> AsyncRx.choose (function | SignIn -> Some login | _ -> None)
            |> AsyncRx.switchLatest
            |> AsyncRx.showSimpleErrorToast (fun e -> "Login failed", e.Message)
            |> AsyncRx.map SignInResult

        let logout =
            AsyncRx.defer (fun () -> AsyncRx.ofPromise (clientApplication.logout()))
            |> AsyncRx.map (Ok >> SignOutResult)
            |> AsyncRx.catch (Result.Error >> SignOutResult >> AsyncRx.single)
        yield
            msgs
            |> AsyncRx.choose (function | SignOut -> Some logout | _ -> None)
            |> AsyncRx.switchLatest
    ]
    |> AsyncRx.mergeSeq
