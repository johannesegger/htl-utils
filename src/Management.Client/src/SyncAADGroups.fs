module SyncAADGroups

open AADGroupUpdates.DataTransferTypes
open Fable.Core
open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open Fable.Reaction
open FSharp.Control
open Fulma
open Fulma.Extensions.Wikiki
open Thoth.Fetch
open Thoth.Json

type UIUserUpdate = {
    IsEnabled: bool
    User: User
}

module UIUserUpdate =
    let fromDto user = { IsEnabled = true; User = user }
    let toDto user = if user.IsEnabled then Some user.User else None

type UIMemberUpdates = {
    AddMembers: UIUserUpdate list
    RemoveMembers: UIUserUpdate list
}

module UIMemberUpdates =
    let fromDto (memberUpdates: MemberUpdates) =
        {
            AddMembers = List.map UIUserUpdate.fromDto memberUpdates.AddMembers
            RemoveMembers = List.map UIUserUpdate.fromDto memberUpdates.RemoveMembers
        }
    let toDto memberUpdates =
        {
            MemberUpdates.AddMembers = List.choose UIUserUpdate.toDto memberUpdates.AddMembers
            MemberUpdates.RemoveMembers = List.choose UIUserUpdate.toDto memberUpdates.RemoveMembers
        }

type UIGroupUpdateType =
    | CreateGroup of string * UIUserUpdate list
    | UpdateGroup of Group * UIMemberUpdates
    | DeleteGroup of Group

module UIGroupUpdateType =
    let fromDto = function
        | GroupUpdate.CreateGroup (name, members) -> CreateGroup (name, List.map UIUserUpdate.fromDto members)
        | GroupUpdate.UpdateGroup (group, memberUpdates) -> UpdateGroup (group, UIMemberUpdates.fromDto memberUpdates)
        | GroupUpdate.DeleteGroup group -> DeleteGroup group
    let toDto = function
        | CreateGroup (name, members) -> GroupUpdate.CreateGroup (name, List.choose UIUserUpdate.toDto members)
        | UpdateGroup (group, memberUpdates) -> GroupUpdate.UpdateGroup (group, UIMemberUpdates.toDto memberUpdates)
        | DeleteGroup group -> GroupUpdate.DeleteGroup group

type UIGroupUpdate = {
    IsEnabled: bool
    Update: UIGroupUpdateType
}

module UIGroupUpdate =
    let fromDto groupUpdate = { IsEnabled = true; Update = UIGroupUpdateType.fromDto groupUpdate }
    let toDto groupUpdate =
        if groupUpdate.IsEnabled then
            Some (UIGroupUpdateType.toDto groupUpdate.Update)
        else None

type UIGroupUpdatesState =
    | Drafting
    | Applying
    | Applied

type UIGroupUpdates =
    | NotLoadedGroupUpdates
    | LoadingGroupUpdates
    | LoadedGroupUpdates of UIGroupUpdatesState * UIGroupUpdate list
    | FailedToLoadGroupUpdates

type Model =
    {
        GroupUpdates: UIGroupUpdates
    }

type Msg =
    | LoadGroupUpdates
    | LoadGroupUpdatesResponse of Result<GroupUpdate list, exn>
    | SelectAllUpdates of bool
    | ToggleEnableGroupUpdate of UIGroupUpdate
    | ToggleEnableMemberUpdate of UIGroupUpdate * UIUserUpdate
    | ApplyGroupUpdates
    | ApplyGroupUpdatesResponse of Result<unit, exn>

let rec update msg model =
    let updateGroupUpdates isMatch fn =
        match model.GroupUpdates with
        | LoadedGroupUpdates (Drafting, groupUpdates) ->
            let groupUpdates' =
                groupUpdates
                |> List.map (fun p -> if isMatch p then fn p else p)
            { model with GroupUpdates = LoadedGroupUpdates (Drafting, groupUpdates') }
        | _ -> model

    let updateGroupUpdate groupUpdate fn =
        updateGroupUpdates ((=) groupUpdate) fn

    let updateMemberUpdate groupUpdate memberUpdate fn =
        updateGroupUpdate groupUpdate (fun groupUpdate ->
            let updateMemberUpdate memberUpdates fn =
                memberUpdates
                |> List.map (fun m -> if m = memberUpdate then fn m else m)
            let update' =
                match groupUpdate.Update with
                | CreateGroup (name, memberUpdates) ->
                    let memberUpdates' = updateMemberUpdate memberUpdates fn
                    CreateGroup (name, memberUpdates')
                | UpdateGroup (group, memberUpdates) ->
                    let memberUpdates' =
                        { memberUpdates with
                            AddMembers = updateMemberUpdate memberUpdates.AddMembers fn
                            RemoveMembers = updateMemberUpdate memberUpdates.RemoveMembers fn
                        }
                    UpdateGroup (group, memberUpdates')
                | DeleteGroup group -> DeleteGroup group
            { groupUpdate with Update = update' }
        )

    match msg with
    | LoadGroupUpdates -> { model with GroupUpdates = LoadingGroupUpdates }
    | LoadGroupUpdatesResponse (Ok groupUpdates) ->
        { model with GroupUpdates = LoadedGroupUpdates (Drafting, List.map UIGroupUpdate.fromDto groupUpdates) }
    | LoadGroupUpdatesResponse (Error ex) ->
        { model with GroupUpdates = FailedToLoadGroupUpdates }
    | SelectAllUpdates value ->
        updateGroupUpdates (fun _ -> true) (fun p -> { p with IsEnabled = value })
    | ToggleEnableGroupUpdate groupUpdate ->
        updateGroupUpdate groupUpdate (fun p -> { p with IsEnabled = not p.IsEnabled })
    | ToggleEnableMemberUpdate (groupUpdate, memberUpdate) ->
        updateMemberUpdate groupUpdate memberUpdate (fun p -> { p with IsEnabled = not p.IsEnabled })
    | ApplyGroupUpdates ->
        match model.GroupUpdates with
        | LoadedGroupUpdates (Drafting, groupUpdates) ->
            { model with GroupUpdates = LoadedGroupUpdates (Applying, groupUpdates) }
        | LoadedGroupUpdates (Applying, _)
        | LoadedGroupUpdates (Applied, _)
        | NotLoadedGroupUpdates
        | LoadingGroupUpdates
        | FailedToLoadGroupUpdates -> model
    | ApplyGroupUpdatesResponse (Ok ())
    | ApplyGroupUpdatesResponse (Error _) ->
        match model.GroupUpdates with
        | LoadedGroupUpdates (Applying, groupUpdates) ->
            { model with GroupUpdates = LoadedGroupUpdates (Applied, groupUpdates) }
        | LoadedGroupUpdates (Drafting, _)
        | LoadedGroupUpdates (Applied, _)
        | NotLoadedGroupUpdates
        | LoadingGroupUpdates
        | FailedToLoadGroupUpdates -> model

let init =
    {
        GroupUpdates = NotLoadedGroupUpdates
    }

let view model dispatch =
    let bulkOperations isLocked =
        Button.list [] [
            Button.button
                [
                    Button.Disabled isLocked
                    Button.OnClick (fun e -> dispatch (SelectAllUpdates true))
                ]
                [
                    Icon.icon [] [ Fa.i [ Fa.Solid.CheckSquare ] [] ]
                    span [] [ str "Select all group updates" ]
                ]
            Button.button
                [
                    Button.Disabled isLocked
                    Button.OnClick (fun e -> dispatch (SelectAllUpdates false))
                ]
                [
                    Icon.icon [] [ Fa.i [ Fa.Regular.CheckSquare ] [] ]
                    span [] [ str "Unselect all group updates" ]
                ]
        ]

    let groupUpdate isLocked groupUpdateModel =
        let heading title icon color =
            Panel.heading [] [
                Button.button
                    [
                        Button.Disabled isLocked
                        Button.Size IsSmall
                        Button.Color (if groupUpdateModel.IsEnabled then color else NoColor)
                        Button.Props [ Style [ MarginRight "0.5rem" ] ]
                        Button.OnClick (fun e -> dispatch (ToggleEnableGroupUpdate groupUpdateModel))
                    ]
                    [ Fa.i [ icon ] [] ]
                span [] [ str title ]
            ]

        let memberUpdate icon color (memberUpdateModel: UIUserUpdate) =
            Panel.block [ ] [
                Panel.icon [] []
                Button.button
                    [
                        Button.Disabled (isLocked || not groupUpdateModel.IsEnabled)
                        Button.Size IsSmall
                        Button.Color (if groupUpdateModel.IsEnabled && memberUpdateModel.IsEnabled then color else NoColor)
                        Button.Props [ Style [ MarginRight "0.5rem" ] ]
                        Button.OnClick (fun e -> dispatch (ToggleEnableMemberUpdate (groupUpdateModel, memberUpdateModel)))
                    ]
                    [ Fa.i [ icon ] [] ]
                str (sprintf "%s - %s %s" memberUpdateModel.User.UserName (memberUpdateModel.User.LastName.ToUpper()) memberUpdateModel.User.FirstName)
            ]

        let memberText i =
            if i = 1 then sprintf "%d member" i
            else sprintf "%d members" i

        match groupUpdateModel.Update with
        | CreateGroup (name, memberUpdates) ->
            Panel.panel [ ] [
                let title = sprintf "%s (+%s)" name (memberText memberUpdates.Length)
                heading title Fa.Solid.Plus IsSuccess
                yield! List.map (memberUpdate Fa.Solid.Plus IsSuccess) memberUpdates
            ]
        | UpdateGroup (group, memberUpdates) ->
            Panel.panel [ ] [
                let title = sprintf "%s (+%s, -%s)" group.Name (memberText memberUpdates.AddMembers.Length) (memberText memberUpdates.RemoveMembers.Length)
                heading title Fa.Solid.Sync IsWarning
                yield!
                    let addMembers = List.map (fun m -> (Fa.Solid.Plus, IsSuccess, m)) memberUpdates.AddMembers in
                    let removeMembers = List.map (fun m -> (Fa.Solid.Minus, IsDanger, m)) memberUpdates.RemoveMembers in
                    List.append addMembers removeMembers
                    |> List.sortBy (fun (_, _, m) -> (m.User.LastName, m.User.FirstName))
                    |> List.map (fun (icon, color, update) -> memberUpdate icon color update)
            ]
        | DeleteGroup group ->
            Panel.panel [ ] [
                heading group.Name Fa.Solid.Minus IsDanger
            ]

    let updates =
        match model.GroupUpdates with
        | NotLoadedGroupUpdates
        | LoadingGroupUpdates ->
            Section.section [] [
                Progress.progress [ Progress.Color IsDanger ] []
            ]
        | FailedToLoadGroupUpdates ->
            Section.section [] [ Views.errorWithRetryButton "Error while loading group updates" (fun () -> dispatch LoadGroupUpdates) ]
        | LoadedGroupUpdates (state, updates) ->
            let isLocked =
                match state with
                | Drafting -> false
                | Applying
                | Applied -> true
            Section.section [] [
                bulkOperations isLocked
                yield!
                    updates
                    |> List.map (groupUpdate isLocked)
                    |> List.intersperse (Divider.divider [])
            ]

    let buttons =
        match model.GroupUpdates with
        | NotLoadedGroupUpdates
        | LoadingGroupUpdates
        | FailedToLoadGroupUpdates -> None
        | LoadedGroupUpdates (state, _) ->
            Section.section [] [
                Field.div [ Field.IsGrouped ] [
                    Control.div [] [
                        Button.button
                            [
                                let isLocked =
                                    match state with
                                    | Applying -> true
                                    | Drafting
                                    | Applied -> false
                                Button.Disabled isLocked
                                Button.OnClick (fun e -> dispatch LoadGroupUpdates)
                            ]
                            [
                                Icon.icon [] [ Fa.i [ Fa.Solid.Sync ] [] ]
                                span [] [ str "Reload AAD group updates" ]
                            ]
                    ]
                    Control.div [] [
                        Button.button
                            [
                                Button.Disabled (match state with | Applied -> true | Drafting | Applying -> false)
                                Button.IsLoading (match state with | Applying -> true | Drafting | Applied -> false)
                                Button.Color IsSuccess
                                Button.OnClick (fun e -> dispatch ApplyGroupUpdates)
                            ]
                            [
                                Icon.icon [] [ Fa.i [ Fa.Solid.Save ] [] ]
                                span [] [ str "Apply updates" ]
                            ]
                    ]
                ]
            ]
            |> Some

    Container.container [] [
        updates
        yield! Option.toList buttons
    ]

let stream getAuthRequestHeader (pageActive: IAsyncObservable<bool>) (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
    pageActive
    |> AsyncRx.flatMapLatest (function
        | true ->
            [
                msgs

                let loadGroupUpdates =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync (async {
                            let! authHeader = getAuthRequestHeader ()
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            let! updates = Fetch.tryGet("/api/aad/group-updates", Decode.list GroupUpdate.decoder, requestProperties) |> Async.AwaitPromise
                            match updates with
                            | Ok v -> return v
                            | Error e -> return failwith (String.ellipsis 200 e)
                        })
                        |> AsyncRx.map Ok
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )

                states
                |> AsyncRx.choose (fst >> function | Some LoadGroupUpdates -> Some loadGroupUpdates | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showSimpleErrorToast (fun e -> "Loading AAD group updates failed", e.Message)
                |> AsyncRx.map LoadGroupUpdatesResponse

                AsyncRx.single LoadGroupUpdates

                let applyGroupUpdates groupUpdates =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync (async {
                            let url = sprintf "/api/aad/group-updates/apply"
                            let data = (List.map GroupUpdate.encode >> Encode.list) groupUpdates
                            let! authHeader = getAuthRequestHeader ()
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            return! Fetch.post(url, data, Decode.nil (), requestProperties) |> Async.AwaitPromise
                        })
                        |> AsyncRx.map Ok
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )

                msgs
                |> AsyncRx.withLatestFrom (AsyncRx.map snd states)
                |> AsyncRx.choose (function
                    | (ApplyGroupUpdates, { GroupUpdates = LoadedGroupUpdates (_, groupUpdates) }) ->
                        Some (applyGroupUpdates (List.choose UIGroupUpdate.toDto groupUpdates))
                    | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showSimpleErrorToast (fun e -> "Applying AAD group updates failed", e.Message)
                |> AsyncRx.showSimpleSuccessToast (fun () -> "Applying AAD group updates", "Successfully applied AAD group updates")
                |> AsyncRx.map ApplyGroupUpdatesResponse
            ]
            |> AsyncRx.mergeSeq
        | false -> AsyncRx.empty ()
    )
