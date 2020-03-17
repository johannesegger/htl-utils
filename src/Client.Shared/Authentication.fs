module Authentication

open Fable.Core
open Fable.Core.JsInterop
open Fable.FontAwesome
open Fable.React
open Fable.Reaction
open FSharp.Control
open Fulma

type User = { Name: string }

type Model =
    | Loading
    | NotAuthenticated
    | Authenticated of User

type Msg =
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

let private getAuthenticatedUser () = promise {
    match userAgentApplication.getAccount() |> Option.ofObj with
    | Some account ->
        return { Name = account.name }
    | None ->
        let authParams = createEmpty<Msal.AuthenticationParameters>
        // authParams.scopes <- Some (ResizeArray [| "contacts.readwrite" |])
        let! authResponse = userAgentApplication.loginPopup authParams
        return { Name = authResponse.account.name }
}

[<Emit("$0.name === \"InteractionRequiredAuthError\"")>]
let private isInteractionRequiredAuthError (_ : exn) : bool = jsNative

let tryGetToken() = async {
    let authParams = createEmpty<Msal.AuthenticationParameters>
    authParams.scopes <- Some !![| appId |]
    try
        let! authResponse = userAgentApplication.acquireTokenSilent authParams |> Async.AwaitPromise
        return Some authResponse.accessToken
    with
        | error when isInteractionRequiredAuthError error ->
            let! authResponse = userAgentApplication.acquireTokenPopup authParams |> Async.AwaitPromise
            return Some authResponse.accessToken
        | error ->
            Browser.Dom.console.log("Auth error", error)
            return None
}

let tryGetRequestHeader () = async {
    let! token = tryGetToken ()
    return
        token
        |> Option.map (sprintf "Bearer %s" >> Fetch.Types.Authorization)
}

let tryGetLoggedInUser model =
    match model with
    | Loading
    | NotAuthenticated -> None
    | Authenticated user -> Some user

let rec update msg model =
    match msg with
    | SignIn -> model
    | SignInResult (Ok user) -> Authenticated user
    | SignInResult (Error _e) -> NotAuthenticated
    | SignOut -> model
    | SignOutResult  (Ok ()) -> NotAuthenticated
    | SignOutResult  (Error _e) -> NotAuthenticated

let init = Loading

let view model dispatch =
    match model with
    | Loading ->
        Button.button [ Button.IsLoading true ] []
    | NotAuthenticated ->
        Button.button
            [ Button.OnClick (fun _e -> dispatch SignIn) ]
            [
                Icon.icon [] [ Fa.i [ Fa.Brand.Windows ] [] ]
                span [] [ str "Sign in" ]
            ]
    | Authenticated user ->
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
                        AsyncRx.ofAsync' (async {
                            let! token = tryGetToken ()
                            match token with
                            | Some _ -> return { Name = user.name }
                            | None -> return failwith "Please sign in using your Microsoft account."
                        })
                        |> AsyncRx.map (Ok >> SignInResult)
                        |> AsyncRx.catch (Error >> SignInResult >> AsyncRx.single)
                        |> Some
                    | None ->
                        AsyncRx.single (SignInResult (Error (exn "Not signed in")))
                        |> Some
                | _ -> None
            )
            |> AsyncRx.switchLatest

        let login =
            AsyncRx.defer (getAuthenticatedUser >> AsyncRx.ofPromise)
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
