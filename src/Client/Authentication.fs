module Authentication

open Elmish
open Fable.Core.JsInterop
open Fable.Helpers.React
open Fable.PowerPack
open Fable.PowerPack.Fetch
open Fulma
open Fulma.FontAwesome

type User =
    { Name: string }

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

let userAgentApplication =
    let clientId = "f2ac1c2a-f1cf-40cb-891b-192c74a096a4"
    let authority = "https://login.microsoftonline.com/htlvb.at"
    // Not called with `loginPopup`
    let tokenReceivedCallBack errorDesc token error tokenType userState =
        //printfn "===== TOKEN RECEIVED: %s - %s - %s - %s - %s" errorDesc token error tokenType userState
        ()
    Msal.UserAgentApplication.Create(clientId, Some authority, tokenReceivedCallBack)

let authHeaderOptFn model =
    let getToken() = promise {
        let scope = [| "f2ac1c2a-f1cf-40cb-891b-192c74a096a4" |]
        try
            return! userAgentApplication.acquireTokenSilent !!scope
        with _error ->
            try
                return! userAgentApplication.acquireTokenPopup !!scope
            with _error ->
                return failwith "Please sign in using your Microsoft account."
    }

    let getAuthHeader() = promise {
        let! token = getToken()
        return Authorization ("Bearer " + token)
    }

    match model with
    | NotAuthenticated -> None
    | Authenticated _ -> Some getAuthHeader

let mapToUser (msalUser: Msal.User) =
    { Name = msalUser.name }

let rec update msg model =
    match msg with
    | Init ->
        match userAgentApplication.getUser() |> Option.ofObj with
        | Some user ->
            update (SignInResult (Ok { Name = user.name })) model
        | None -> model, Cmd.none
    | SignIn ->
        let cmd =
            Cmd.ofPromise
                userAgentApplication.loginPopup
                !![| "contacts.readwrite"; "calendars.readwrite" |]
                ((fun _ -> userAgentApplication.getUser()) >> mapToUser >> Ok >> SignInResult)
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
            [ Icon.faIcon [] [ Fa.icon Fa.I.Windows ]
              span [] [ str "Sign in" ] ]
    | Authenticated user ->
        Button.button
            [ Button.OnClick (fun _e -> dispatch SignOut) ]
            [ Icon.faIcon [] [ Fa.icon Fa.I.Windows ]
              span [] [ str (sprintf "%s | Sign out" user.Name) ] ]