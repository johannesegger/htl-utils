module SyncAADGroups

open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open Fable.Reaction
open FSharp.Control
open Fulma
open Fulma.Extensions.Wikiki
open Shared
open Thoth.Fetch
open Thoth.Json

type UserUpdate = {
    IsEnabled: bool
    User: AADGroupUpdates.User
}

module UserUpdate =
    let fromDto user = { IsEnabled = true; User = user }
    let toDto user = if user.IsEnabled then Some user.User else None

type MemberUpdates = {
    AddMembers: UserUpdate list
    RemoveMembers: UserUpdate list
}

module MemberUpdates =
    let fromDto (memberUpdates: AADGroupUpdates.MemberUpdates) =
        {
            AddMembers = List.map UserUpdate.fromDto memberUpdates.AddMembers
            RemoveMembers = List.map UserUpdate.fromDto memberUpdates.RemoveMembers
        }
    let toDto memberUpdates =
        {
            AADGroupUpdates.MemberUpdates.AddMembers = List.choose UserUpdate.toDto memberUpdates.AddMembers
            AADGroupUpdates.MemberUpdates.RemoveMembers = List.choose UserUpdate.toDto memberUpdates.RemoveMembers
        }

type GroupUpdateType =
    | CreateGroup of string * UserUpdate list
    | UpdateGroup of AADGroupUpdates.Group * MemberUpdates
    | DeleteGroup of AADGroupUpdates.Group

module GroupUpdateType =
    let fromDto = function
        | AADGroupUpdates.CreateGroup (name, members) -> CreateGroup (name, List.map UserUpdate.fromDto members)
        | AADGroupUpdates.UpdateGroup (group, memberUpdates) -> UpdateGroup (group, MemberUpdates.fromDto memberUpdates)
        | AADGroupUpdates.DeleteGroup group -> DeleteGroup group
    let toDto = function
        | CreateGroup (name, members) -> AADGroupUpdates.CreateGroup (name, List.choose UserUpdate.toDto members)
        | UpdateGroup (group, memberUpdates) -> AADGroupUpdates.UpdateGroup (group, MemberUpdates.toDto memberUpdates)
        | DeleteGroup group -> AADGroupUpdates.DeleteGroup group

type GroupUpdate = {
    IsEnabled: bool
    Update: GroupUpdateType
}

module GroupUpdate =
    let fromDto groupUpdate = { IsEnabled = true; Update = GroupUpdateType.fromDto groupUpdate }
    let toDto groupUpdate =
        if groupUpdate.IsEnabled then
            Some (GroupUpdateType.toDto groupUpdate.Update)
        else None

type GroupUpdatesState =
    | Drafting
    | Applying
    | Applied

type GroupUpdates =
    | NotLoadedGroupUpdates
    | LoadingGroupUpdates
    | LoadedGroupUpdates of GroupUpdatesState * GroupUpdate list
    | FailedToLoadGroupUpdates

type Model =
    {
        GroupUpdates: GroupUpdates
    }

type Msg =
    | LoadGroupUpdates
    | LoadGroupUpdatesResponse of Result<AADGroupUpdates.GroupUpdate list, exn>
    | SelectAllUpdates of bool
    | ToggleEnableGroupUpdate of GroupUpdate
    | ToggleEnableMemberUpdate of GroupUpdate * UserUpdate
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
        { model with GroupUpdates = LoadedGroupUpdates (Drafting, List.map GroupUpdate.fromDto groupUpdates) }
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

        let memberUpdate icon color (memberUpdateModel: UserUpdate) =
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
                str (sprintf "%s - %s %s" memberUpdateModel.User.ShortName (memberUpdateModel.User.LastName.ToUpper()) memberUpdateModel.User.FirstName)
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
                Progress.progress
                    [
                        Progress.Color IsDanger
                        Progress.Max 100
                    ]
                    [ str "0%" ]
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

let stream authHeader states (msgs: IAsyncObservable<_>) =
    authHeader
    |> AsyncRx.choose id
    |> AsyncRx.flatMapLatest (fun authHeader ->
        [
            msgs

            let loadGroupUpdates =
                AsyncRx.defer (fun () ->
                    AsyncRx.ofPromise (promise {
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        let! updates = Fetch.tryGet("/api/aad/group-updates", Decode.list AADGroupUpdates.GroupUpdate.decoder, requestProperties)
                        match updates with
                        | Ok v -> return v
                        | Error e ->
                            let msg =
                                if e.Length > 200
                                then sprintf "%s ..." <| e.Substring(0, 197)
                                else e
                            return failwith msg
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
                    AsyncRx.ofPromise (promise {
                        let url = sprintf "/api/aad/group-updates/apply"
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        let data = (List.map AADGroupUpdates.GroupUpdate.encode >> Encode.list) groupUpdates
                        return! Fetch.post(url, data, Decode.nil (), requestProperties)
                    })
                    |> AsyncRx.map Ok
                    |> AsyncRx.catch (Error >> AsyncRx.single)
                )

            msgs
            |> AsyncRx.withLatestFrom (AsyncRx.map snd states)
            |> AsyncRx.choose (function
                | (ApplyGroupUpdates, { GroupUpdates = LoadedGroupUpdates (_, groupUpdates) }) ->
                    Some (applyGroupUpdates (List.choose GroupUpdate.toDto groupUpdates))
                | _ -> None)
            |> AsyncRx.switchLatest
            |> AsyncRx.showSimpleErrorToast (fun e -> "Applying AAD group updates failed", e.Message)
            |> AsyncRx.showSimpleSuccessToast (fun () -> "Applying AAD group updates", "Successfully applied AAD group updates")
            |> AsyncRx.map ApplyGroupUpdatesResponse
        ]
        |> AsyncRx.mergeSeq
    )
