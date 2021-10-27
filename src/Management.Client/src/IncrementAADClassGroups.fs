module IncrementAADClassGroups

open Fable.Core
open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open FSharp.Control
open Fulma
open IncrementClassGroups.DataTransferTypes
open Thoth.Fetch
open Thoth.Json

type UIClassGroupModificationGroup = {
    IsEnabled: bool
    Title: string
    Modifications: ClassGroupModification list
}
module UIClassGroupModificationGroup =
    let fromDto (modificationGroup: ClassGroupModificationGroup) =
        {
            IsEnabled = true
            Title = modificationGroup.Title
            Modifications = modificationGroup.Modifications
        }
    let toModificationDtos modificationGroup =
        if modificationGroup.IsEnabled then modificationGroup.Modifications
        else []

type ModificationsState =
    | Drafting
    | Applying of ClassGroupModification list
    | Applied
module ModificationsState =
    let isDrafting = function | Drafting -> true | _ -> false
    let isApplying = function | Applying _ -> true | _ -> false
    let isApplied = function | Applied -> true | _ -> false

type UIModifications = {
    State: ModificationsState
    ModificationGroups: UIClassGroupModificationGroup list
}

type LoadableClassGroups =
    | LoadingModifications
    | LoadedModifications of UIModifications
    | FailedToLoadModifications

type Model = LoadableClassGroups

type Msg =
    | LoadModifications
    | LoadModificationsResponse of Result<ClassGroupModificationGroup list, exn>
    | SelectAllModifications of bool
    | ToggleEnableModificationGroup of UIClassGroupModificationGroup
    | ApplyModifications
    | ApplyModificationsResponse of Result<unit, exn>

let init = LoadingModifications

let update msg model =
    let mapLoadedModifications fn =
        match model with
        | LoadedModifications modifications -> LoadedModifications (fn modifications)
        | LoadingModifications
        | FailedToLoadModifications -> model
    let mapLoadedModificationGroups fn =
        mapLoadedModifications (fun modifications -> { modifications with ModificationGroups = fn modifications.ModificationGroups })
    match msg with
    | LoadModifications -> LoadingModifications
    | LoadModificationsResponse (Ok modificationGroups) ->
        LoadedModifications {
            State = Drafting
            ModificationGroups = List.map UIClassGroupModificationGroup.fromDto modificationGroups
        }
    | LoadModificationsResponse (Error _) -> FailedToLoadModifications
    | SelectAllModifications v ->
        mapLoadedModificationGroups (fun modificationGroups ->
            modificationGroups
            |> List.map (fun group -> { group with IsEnabled = v })
        )
    | ToggleEnableModificationGroup modificationGroup ->
        mapLoadedModificationGroups (fun modificationGroups ->
            modificationGroups
            |> List.map (fun group ->
                if modificationGroup = group then { group with IsEnabled = not group.IsEnabled }
                else group
            )
        )
    | ApplyModifications ->
        mapLoadedModifications (fun modifications ->
            { modifications with
                State =
                    Applying (
                        modifications.ModificationGroups
                        |> List.collect UIClassGroupModificationGroup.toModificationDtos
                    )
            }
        )
    | ApplyModificationsResponse (Ok ())
    | ApplyModificationsResponse (Error _) ->
        mapLoadedModifications (fun modifications -> { modifications with State = Applied })

let view model dispatch =
    let isLocked =
        match model with
        | LoadingModifications
        | FailedToLoadModifications
        | LoadedModifications { State = Applying _ }
        | LoadedModifications { State = Applied } -> true
        | LoadedModifications { State = Drafting } -> false

    let bulkOperations =
        Button.list [] [
            Button.button
                [
                    Button.Disabled isLocked
                    Button.OnClick (fun e -> dispatch (SelectAllModifications true))
                ]
                [
                    Icon.icon [] [ Fa.i [ Fa.Solid.CheckSquare ] [] ]
                    span [] [ str "Select all modifications" ]
                ]
            Button.button
                [
                    Button.Disabled isLocked
                    Button.OnClick (fun e -> dispatch (SelectAllModifications false))
                ]
                [
                    Icon.icon [] [ Fa.i [ Fa.Regular.CheckSquare ] [] ]
                    span [] [ str "Unselect all modifications" ]
                ]
        ]

    let modificationView modification =
        let description =
            match modification with
            | ChangeClassGroupName (oldName, newName) ->
                sprintf "%s -> %s" oldName newName
            | DeleteClassGroup name ->
                sprintf "%s -> *" name
        Panel.Block.div [] [
            str description
        ]

    let modificationGroupView modificationGroup =
        Panel.panel [] [
            Panel.heading [] [
                Button.button
                    [
                        Button.Disabled isLocked
                        Button.Size IsSmall
                        Button.Color (if modificationGroup.IsEnabled then IsWarning else NoColor)
                        Button.Props [ Style [ MarginRight "0.5rem" ] ]
                        Button.OnClick (fun _ -> dispatch (ToggleEnableModificationGroup modificationGroup))
                    ]
                    [ Fa.i [ Fa.Solid.Sync ] [] ]
                span [] [ str modificationGroup.Title ]
            ]
            yield! List.map modificationView modificationGroup.Modifications
        ]

    Container.container [] [
        match model with
        | LoadingModifications ->
            Section.section [] [
                Progress.progress [ Progress.Color IsDanger ] []
            ]
        | FailedToLoadModifications ->
            Section.section [] [ Views.errorWithRetryButton "Error while loading modifications" (fun () -> dispatch LoadModifications) ]
        | LoadedModifications modifications ->
            Section.section [] [
                Views.warning "Mail addresses and proxy addresses can't be updated. However that can be done using `Set-UnifiedGroup` in Exchange PowerShell."
                bulkOperations
                yield! List.map modificationGroupView modifications.ModificationGroups
                Button.list [] [
                    Button.button
                        [
                            Button.Disabled (ModificationsState.isApplying modifications.State)
                            Button.OnClick (fun e -> dispatch LoadModifications)
                        ]
                        [
                            Icon.icon [] [ Fa.i [ Fa.Solid.Sync ] [] ]
                            span [] [ str "Reload modifications" ]
                        ]
                    Button.button
                        [
                            Button.Disabled (ModificationsState.isApplied modifications.State)
                            Button.IsLoading (ModificationsState.isApplying modifications.State)
                            Button.Color IsSuccess
                            Button.OnClick (fun _ -> dispatch ApplyModifications)
                        ]
                        [
                            Icon.icon [] [ Fa.i [ Fa.Solid.Save ] [] ]
                            span [] [ str "Apply modifications" ]
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

                let loadUpdates =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync (async {
                            let! authHeader = getAuthRequestHeader ()
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            let coders = Extra.empty |> Thoth.addCoders
                            let! (modifications: ClassGroupModificationGroup list) = Fetch.get("/api/aad/increment-class-group-updates", properties = requestProperties, extra = coders) |> Async.AwaitPromise
                            return modifications
                        })
                        |> AsyncRx.map Ok
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )

                msgs
                |> AsyncRx.startWith [ LoadModifications ]
                |> AsyncRx.choose (function | LoadModifications -> Some loadUpdates | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showSimpleErrorToast (fun e -> "Loading AAD modifications failed", e.Message)
                |> AsyncRx.map LoadModificationsResponse

                let applyModifications modifications =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync (async {
                            let url = sprintf "/api/aad/increment-class-group-updates/apply"
                            let! authHeader = getAuthRequestHeader ()
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            let coders = Extra.empty |> Thoth.addCoders
                            do! Fetch.post(url, modifications, properties = requestProperties, extra = coders) |> Async.AwaitPromise
                        })
                        |> AsyncRx.map Ok
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )

                states
                |> AsyncRx.choose (function
                    | Some ApplyModifications, LoadedModifications { State = Applying modifications } -> Some (applyModifications modifications)
                    | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showSimpleErrorToast (fun e -> "Applying AAD modifications failed", e.Message)
                |> AsyncRx.showSimpleSuccessToast (fun () -> "Applying AAD modifications", "Successfully applied AAD modifications")
                |> AsyncRx.map ApplyModificationsResponse
            ]
            |> AsyncRx.mergeSeq
        | false -> AsyncRx.empty ()
    )
