module Authentication

open Elmish
open Elmish.Streams
open Fable.Core.JsInterop
open Fable.FontAwesome
open Fable.React
open FSharp.Control
open Fulma
open Thoth.Elmish

type User =
    {
        Name: string
        Token: string
    }

type Model =
    | Loading
    | NotAuthenticated
    | Authenticated of User

type Msg =
    | SignIn
    | SignInResult of Result<User, exn>
    | SignOut
    | SignOutResult of Result<unit, exn>

let appId = "9fb9b79b-6e66-4007-a94f-571d7e3b68c5"
let userAgentApplication =
    let options =
        let cacheOptions = Fable.Core.JsInterop.createEmpty<Msal.CacheOptions>
        cacheOptions.cacheLocation <- Some Msal.CacheLocation.LocalStorage

        let authOptions = Fable.Core.JsInterop.createEmpty<Msal.AuthOptions>
        authOptions.authority <- Some "https://login.microsoftonline.com/htlvb.at/"
        authOptions.clientId <- appId

        let o = Fable.Core.JsInterop.createEmpty<Msal.Configuration>
        o.cache <- Some cacheOptions
        o.auth <- authOptions
        o
    Msal.UserAgentApplication.Create(options)

userAgentApplication.handleRedirectCallback(fun error response ->
    Browser.Dom.console.log("handleRedirectCallback", error, response)
)

let getToken() = promise {
    let authParams = Fable.Core.JsInterop.createEmpty<Msal.AuthenticationParameters>
    authParams.scopes <- Some !![| appId |]
    try
        let! authResponse = userAgentApplication.acquireTokenSilent authParams
        return authResponse.accessToken
    with error ->
        try
            // if error :? Msal.InteractionRequiredAuthError then
            let! authResponse = userAgentApplication.acquireTokenPopup authParams
            return authResponse.accessToken
            // else
            //     return reraise()
        with _error ->
            return failwith "Please sign in using your Microsoft account."
}

let tryGetAuthHeader model =
    match model with
    | Loading
    | NotAuthenticated -> None
    | Authenticated user -> Some (Fetch.Types.Authorization ("Bearer " + user.Token))

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
            |> AsyncRx.distinctUntilChanged
            |> AsyncRx.choose (function
                | Loading ->
                    match userAgentApplication.getAccount() |> Option.ofObj with
                    | Some user ->
                        AsyncRx.ofPromise (promise {
                            let! token = getToken()
                            return { Name = user.name; Token = token }
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
            AsyncRx.defer (fun () ->
                AsyncRx.ofPromise (promise {
                    let authParams = Fable.Core.JsInterop.createEmpty<Msal.AuthenticationParameters>
                    authParams.scopes <- Some (ResizeArray [| "contacts.readwrite" |])
                    let! authResponse = userAgentApplication.loginPopup authParams
                    let! token = getToken()
                    return { Name = userAgentApplication.getAccount().name; Token = token }
                })
            )
            |> AsyncRx.map Ok
            |> AsyncRx.catch (Error >> AsyncRx.single)
        let loginResponseToast response =
            match response with
            | Ok user -> Cmd.none
            | Error (e: exn) ->
                Toast.toast "Wake up failed" e.Message
                |> Toast.error
        yield
            msgs
            |> AsyncRx.choose (function | SignIn -> Some login | _ -> None)
            |> AsyncRx.switchLatest
            |> AsyncRx.showToast loginResponseToast
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
