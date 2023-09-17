module ITInformationSheet

open Fable.Core
open Fable.Core.JS
open Fable.Core.JsInterop
open Fable.React
open FSharp.Control
open Fulma
open Fulma.Extensions.Wikiki
open GenerateITInformationSheet.DataTransferTypes
open Thoth.Fetch
open Thoth.Json

type GenerationState =
    | NotGenerated
    | Generating

type UIUser = {
    GenerationState: GenerationState
    User: User
}
module UIUser =
    let fromDto user =
        {
            GenerationState = NotGenerated
            User = user
        }
    let toDto user = user.User

type LoadableUsers =
    | LoadingUsers
    | LoadedUsers of UIUser list
    | FailedToLoadUsers

type Model = LoadableUsers

type Msg =
    | LoadUsers
    | LoadUsersResponse of Result<User list, exn>
    | GenerateInformationSheet of UIUser
    | GenerateInformationSheetResponse of User * Result<InformationSheet, exn>

let init = LoadingUsers

let update msg model =
    let mapLoadedUsers fn =
        match model with
        | LoadedUsers users -> LoadedUsers (fn users)
        | LoadingUsers
        | FailedToLoadUsers -> model
    let mapLoadedUser user fn =
        mapLoadedUsers (fun users ->
            users
            |> List.map (fun p -> if p = user then fn p else p)
        )
    match msg with
    | LoadUsers -> LoadingUsers
    | LoadUsersResponse (Ok users) ->
        let uiUsers =
            users
            |> List.sortBy (fun p -> p.LastName, p.FirstName, p.ShortName)
            |> List.map UIUser.fromDto
        LoadedUsers uiUsers
    | LoadUsersResponse (Error _) -> FailedToLoadUsers
    | GenerateInformationSheet { GenerationState = Generating } -> model
    | GenerateInformationSheet ({ GenerationState = NotGenerated } as user) ->
        mapLoadedUser user (fun p -> { p with GenerationState = Generating })
    | GenerateInformationSheetResponse (user, Ok _)
    | GenerateInformationSheetResponse (user, Error _) ->
        mapLoadedUser { GenerationState = Generating; User = user } (fun p -> { p with GenerationState = NotGenerated })

let view model dispatch =
    Container.container [] [
        match model with
        | LoadingUsers ->
            Section.section [] [
                Progress.progress [ Progress.Color IsDanger ] []
            ]
        | FailedToLoadUsers ->
            Section.section [] [ Views.errorWithRetryButton "Error while loading users" (fun () -> dispatch LoadUsers) ]
        | LoadedUsers users ->
            Section.section [] [
                for (groupKey, groupUsers) in users |> List.groupBy (fun p -> p.User.LastName |> Seq.tryHead |> Option.map (sprintf "%c") |> Option.defaultValue "<empty>") do
                    yield Divider.divider [ Divider.Label groupKey ]
                    yield Button.list [] [
                        for user in groupUsers ->
                            Button.button
                                [
                                    Button.IsLoading (user.GenerationState = Generating)
                                    Button.Color IsInfo
                                    Button.OnClick (fun _e -> dispatch (GenerateInformationSheet user))
                                ]
                                [
                                    str (sprintf "%s %s (%s)" user.User.LastName user.User.FirstName user.User.ShortName)
                                ]
                    ]
            ]
    ]

let stream getAuthRequestHeader (pageActive: IAsyncObservable<bool>) (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
    pageActive
    |> AsyncRx.flatMapLatest (function
        | true ->
            [
                msgs

                let loadUsers =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync (async {
                            let! authHeader = getAuthRequestHeader ()
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            let coders = Extra.empty |> Thoth.addCoders
                            let! (users: User list) = Fetch.get("/api/it-information/users", properties = requestProperties, extra = coders) |> Async.AwaitPromise
                            return users
                        })
                        |> AsyncRx.map Ok
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )

                msgs
                |> AsyncRx.startWith [ LoadUsers ]
                |> AsyncRx.choose (function | LoadUsers -> Some loadUsers | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showSimpleErrorToast (fun e -> "Loading users failed", e.Message)
                |> AsyncRx.map LoadUsersResponse

                let generateInformationSheet (user: User) =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync (async {
                            let! authHeader = getAuthRequestHeader ()
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            let coders = Extra.empty |> Thoth.addCoders
                            let! (informationSheet: InformationSheet) = Fetch.post("/api/it-information/generate-sheet", data = user, properties = requestProperties, extra = coders) |> Async.AwaitPromise
                            return informationSheet
                        })
                        |> AsyncRx.map Ok
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )

                let downloadInformationSheet v =
                    let (Base64EncodedContent data) = v.Content
                    let decoded = Browser.Dom.window.atob data
                    let buffer = Constructors.ArrayBuffer.Create(decoded.Length)
                    let array = Constructors.Uint8Array.Create buffer
                    Seq.init decoded.Length id
                    |> Seq.iter (fun i -> array.[i] <- uint8 decoded.[i])

                    let a = Browser.Dom.window.document.createElement("a") :?> Browser.Types.HTMLAnchorElement
                    let blobProperties = createEmpty<Browser.Types.BlobPropertyBag>
                    blobProperties.``type`` <- "application/octet-stream"
                    a.href <- Browser.Dom.window?URL?createObjectURL(Browser.Blob.Blob.Create([|array|], blobProperties))
                    a?download <- v.Title
                    Browser.Dom.document.body.appendChild(a) |> ignore
                    try
                        a.click()
                    finally
                        Browser.Dom.document.body.removeChild(a) |> ignore

                msgs
                |> AsyncRx.flatMap (function
                    | GenerateInformationSheet user ->
                        generateInformationSheet user.User
                        |> AsyncRx.showSimpleErrorToast (fun e -> sprintf "Generating information sheet for %s failed" user.User.ShortName, e.Message)
                        |> AsyncRx.showSimpleSuccessToast (fun _ -> "Generating information sheet for", sprintf "Successfully generated information sheet for %s" user.User.ShortName)
                        |> AsyncRx.tapOnNext(fun v ->
                            match v with
                            | Ok v -> downloadInformationSheet v
                            | Error _ -> ()
                        )
                        |> AsyncRx.map (fun p -> GenerateInformationSheetResponse (user.User, p))
                    | _ -> AsyncRx.empty ()
                )
            ]
            |> AsyncRx.mergeSeq
        | false -> AsyncRx.empty ()
    )
