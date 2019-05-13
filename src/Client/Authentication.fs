module Authentication

open Elmish
open Fable.Core.JsInterop
open Fable.Helpers.React
open Fable.PowerPack
open Fable.PowerPack.Fetch
open Fulma
open Fable.FontAwesome

type User =
    {
        Name: string
        Token: string
    }

type Authentication =
    | NotAuthenticated
    | Authenticated of User

type Model =
    Authentication

type Msg =
    | Init
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
    Fable.Import.Browser.console.log("handleRedirectCallback", error, response)
)

let getToken() = promise {
    let authParams = Fable.Core.JsInterop.createEmpty<Msal.AuthenticationParameters>
    authParams.scopes <- Some !![| appId |]
    try
        let! authResponse = userAgentApplication.acquireTokenSilent authParams
        return authResponse.accessToken
    with error ->
        try
            printfn "Error: %A" error
            Fable.Import.Browser.console.log("Error", error)
            // if error :? Msal.InteractionRequiredAuthError then
            let! authResponse = userAgentApplication.acquireTokenPopup authParams
            return authResponse.accessToken
            // else
            //     return reraise()
        with _error ->
            return failwith "Please sign in using your Microsoft account."
}

let authHeaderOptFn model =

    let getAuthHeader() = promise {
        let! token = getToken()
        return Authorization ("Bearer " + token)
    }

    match model with
    | NotAuthenticated -> None
    | Authenticated _ -> Some getAuthHeader

let rec update msg model =
    match msg with
    | Init ->
        let cmd =
            userAgentApplication.getAccount()
            |> Option.ofObj
            |> Option.map (fun user ->
                let getUser () =
                    promise {
                        let! token = getToken()
                        return { Name = user.name; Token = token }
                    }
                Cmd.ofPromise getUser () (Ok >> SignInResult) (Error >> SignInResult)
            )
            |> Option.defaultValue Cmd.none
        model, cmd
    | SignIn ->
        let cmd =
            let authParams = Fable.Core.JsInterop.createEmpty<Msal.AuthenticationParameters>
            authParams.scopes <- Some !![| "contacts.readwrite" |]
            Cmd.ofPromise
                (fun o -> userAgentApplication.loginPopup o)
                authParams
                ((fun authResponse -> { Name = userAgentApplication.getAccount().name; Token = authResponse.accessToken }) >> Ok >> SignInResult)
                (Error >> SignInResult)
        model, cmd
    | SignInResult (Ok user) ->
        let model' = Authenticated user
        model', Cmd.none
    | SignInResult (Error _e) ->
        model, Cmd.none
    | SignOut ->
        let cmd =
            Cmd.ofFunc
                userAgentApplication.logout
                ()
                (Ok >> SignOutResult)
                (Error >> SignOutResult)
        model, cmd
    | SignOutResult  (Ok ()) ->
        let model' = NotAuthenticated
        model', Cmd.none
    | SignOutResult  (Error _e) ->
        model, Cmd.none

let init() =
    update Init NotAuthenticated

let view model dispatch =
    match model with
    | NotAuthenticated ->
        Button.button
            [ Button.OnClick (fun _e -> dispatch SignIn) ]
            [ Icon.icon [] [ Fa.i [ Fa.Brand.Windows ] [] ]
              span [] [ str "Sign in" ] ]
    | Authenticated user ->
        Button.button
            [ Button.OnClick (fun _e -> dispatch SignOut) ]
            [ Icon.icon [] [ Fa.i [ Fa.Brand.Windows ] [] ]
              span [] [ str (sprintf "%s | Sign out" user.Name) ] ]