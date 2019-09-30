module AADGroups

open Browser.Blob
open Elmish
open Elmish.Streams
open Fable.Core
open Fable.Core.JsInterop
open Fable.React
open Fable.React.Props
open Fable.FontAwesome
open Fetch.Types
open FSharp.Control
open Fulma
open Fulma.Extensions.Wikiki
open Thoth.Elmish
open Thoth.Fetch
open Thoth.Json

type File =
    {
        Name: string
        JSFile: Browser.Types.File
    }

type UserUpdate = {
    IsEnabled: bool
    User: Shared.AADGroups.User
}

module UserUpdate =
    let fromDto user = { IsEnabled = true; User = user }
    let toDto user = if user.IsEnabled then Some user.User else None

type MemberUpdates = {
    AddMembers: UserUpdate list
    RemoveMembers: UserUpdate list
}

module MemberUpdates =
    let fromDto (memberUpdates: Shared.AADGroups.MemberUpdates) =
        {
            AddMembers = List.map UserUpdate.fromDto memberUpdates.AddMembers
            RemoveMembers = List.map UserUpdate.fromDto memberUpdates.RemoveMembers
        }
    let toDto memberUpdates =
        {
            Shared.AADGroups.MemberUpdates.AddMembers = List.choose UserUpdate.toDto memberUpdates.AddMembers
            Shared.AADGroups.MemberUpdates.RemoveMembers = List.choose UserUpdate.toDto memberUpdates.RemoveMembers
        }

type GroupUpdateType =
    | CreateGroup of string * UserUpdate list
    | UpdateGroup of Shared.AADGroups.Group * MemberUpdates
    | DeleteGroup of Shared.AADGroups.Group

module GroupUpdateType =
    let fromDto = function
        | Shared.AADGroups.CreateGroup (name, members) -> CreateGroup (name, List.map UserUpdate.fromDto members)
        | Shared.AADGroups.UpdateGroup (group, memberUpdates) -> UpdateGroup (group, MemberUpdates.fromDto memberUpdates)
        | Shared.AADGroups.DeleteGroup group -> DeleteGroup group
    let toDto = function
        | CreateGroup (name, members) -> Shared.AADGroups.CreateGroup (name, List.choose UserUpdate.toDto members)
        | UpdateGroup (group, memberUpdates) -> Shared.AADGroups.UpdateGroup (group, MemberUpdates.toDto memberUpdates)
        | DeleteGroup group -> Shared.AADGroups.DeleteGroup group

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
        UntisTeachingDataFile: File option
        SokratesTeachersFile: File option
        FinalThesesMentorsFile: File option
        GroupUpdates: GroupUpdates
    }

type Msg =
    | SetUntisTeachingDataFile of Browser.Types.File
    | SetSokratesTeachersFile of Browser.Types.File
    | SetFinalThesesMentorsFile of Browser.Types.File
    | LoadGroupUpdates
    | LoadGroupUpdatesResponse of Result<Shared.AADGroups.GroupUpdate list, exn>
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
    | SetUntisTeachingDataFile file ->
        { model with UntisTeachingDataFile = Some { Name = file.name; JSFile = file } }
    | SetSokratesTeachersFile file ->
        { model with SokratesTeachersFile = Some { Name = file.name; JSFile = file } }
    | SetFinalThesesMentorsFile file ->
        { model with FinalThesesMentorsFile = Some { Name = file.name; JSFile = file } }
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
        UntisTeachingDataFile = None
        SokratesTeachersFile = None
        FinalThesesMentorsFile = None
        GroupUpdates = NotLoadedGroupUpdates
    }

let view model dispatch =
    let fileInput label fileName msgFn =
        File.file [ File.HasName; File.IsFullWidth ] [
            File.label [] [
                File.input [ Props [ OnChange (fun e -> dispatch (msgFn (e?currentTarget :> Browser.Types.HTMLInputElement).files.[0])) ] ]
                File.cta [] [
                    File.icon [] [
                        Fa.i [ Fa.Solid.Upload ] []
                    ]
                    span [ Class "file-label" ] [ str label ]
                ]
                File.name [] [ str fileName ]
            ]
        ]

    let form =
        let isLocked =
            match model.GroupUpdates with
            | LoadedGroupUpdates (Applying, _) -> true
            | LoadedGroupUpdates (Drafting, _)
            | LoadedGroupUpdates (Applied, _)
            | NotLoadedGroupUpdates
            | LoadingGroupUpdates
            | FailedToLoadGroupUpdates -> false
        Section.section [] [
            Field.div [] [
                fileInput "Choose Untis teaching data file..." (model.UntisTeachingDataFile |> Option.map (fun t -> t.Name) |> Option.defaultValue "") SetUntisTeachingDataFile
            ]
            Field.div [] [
                fileInput "Choose Sokrates teachers file..." (model.SokratesTeachersFile |> Option.map (fun t -> t.Name) |> Option.defaultValue "") SetSokratesTeachersFile
            ]
            Field.div [] [
                fileInput "Choose final theses mentors file..." (model.FinalThesesMentorsFile |> Option.map (fun t -> t.Name) |> Option.defaultValue "") SetFinalThesesMentorsFile
            ]
            Field.div [] [
                Control.div [] [
                    Button.button
                        [
                            Button.Disabled (isLocked || model.UntisTeachingDataFile.IsNone || model.SokratesTeachersFile.IsNone || model.FinalThesesMentorsFile.IsNone)
                            Button.IsLoading (model.GroupUpdates = LoadingGroupUpdates)
                            Button.OnClick (fun e -> dispatch LoadGroupUpdates)
                        ]
                        [ str "Get AAD group updates" ]
                ]
            ]
        ]

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
                yield heading title Fa.Solid.Plus IsSuccess
                yield! List.map (memberUpdate Fa.Solid.Plus IsSuccess) memberUpdates
            ]
        | UpdateGroup (group, memberUpdates) ->
            Panel.panel [ ] [
                let title = sprintf "%s (+%s, -%s)" group.Name (memberText memberUpdates.AddMembers.Length) (memberText memberUpdates.RemoveMembers.Length)
                yield heading title Fa.Solid.Sync IsWarning
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
        | LoadingGroupUpdates -> None
        | FailedToLoadGroupUpdates ->
            Section.section [] [ Views.errorWithRetryButton "Error while loading group updates" (fun () -> dispatch LoadGroupUpdates) ]
            |> Some
        | LoadedGroupUpdates (state, updates) ->
            let isLocked =
                match state with
                | Drafting -> false
                | Applying
                | Applied -> true
            Section.section [] [
                yield bulkOperations isLocked
                yield!
                    updates
                    |> List.map (groupUpdate isLocked)
                    |> List.intersperse (Divider.divider [])
            ]
            |> Some

    let saveButton =
        match model.GroupUpdates with
        | NotLoadedGroupUpdates
        | LoadingGroupUpdates
        | FailedToLoadGroupUpdates -> None
        | LoadedGroupUpdates (state, _) ->
            Section.section [] [
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
            |> Some

    Container.container [] [
        yield form
        yield! Option.toList updates
        yield! Option.toList saveButton
    ]

let stream authHeader states msgs =
    authHeader
    |> AsyncRx.choose id
    |> AsyncRx.flatMapLatest (fun authHeader ->
        [
            yield msgs

            let loadGroupUpdatesResponseToast response =
                match response with
                | Ok _ -> Cmd.none
                | Error (e: exn) ->
                    Toast.toast "Loading AAD group updates failed" e.Message
                    |> Toast.error

            let loadGroupUpdates (untisTeachingDataFile: Browser.Types.File) (sokratesTeachersFile: Browser.Types.File) (finalThesesMentorsFile: Browser.Types.File) =
                AsyncRx.defer (fun () ->
                    AsyncRx.ofPromise (promise {
                        let url = sprintf "/api/aad/group-updates"
                        let formData = FormData.Create()
                        formData.append("untis-teaching-data", untisTeachingDataFile)
                        formData.append("sokrates-teachers", sokratesTeachersFile)
                        formData.append("final-theses-mentors", finalThesesMentorsFile)
                        let requestProperties = [
                            Method HttpMethod.POST
                            Fetch.requestHeaders [ authHeader ]
                            Body (U3.Case2 formData)
                        ]
                        let! response = Fetch.fetch url requestProperties
                        let! body = response.text()
                        return Decode.unsafeFromString (Decode.list Shared.AADGroups.GroupUpdate.decode) body
                    })
                    |> AsyncRx.map Ok
                    |> AsyncRx.catch (Error >> AsyncRx.single)
                )

            yield
                msgs
                |> AsyncRx.withLatestFrom states
                |> AsyncRx.choose (function
                    | (LoadGroupUpdates, { UntisTeachingDataFile = Some untisTeachingDataFile; SokratesTeachersFile = Some sokratesTeachersFile; FinalThesesMentorsFile = Some finalThesesMentorsFile }) ->
                        Some (loadGroupUpdates untisTeachingDataFile.JSFile sokratesTeachersFile.JSFile finalThesesMentorsFile.JSFile)
                    | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showToast loadGroupUpdatesResponseToast
                |> AsyncRx.map LoadGroupUpdatesResponse

            let applyGroupUpdatesResponseToast response =
                match response with
                | Ok _ ->
                    Toast.toast "Applying AAD group updates" "Successfully applied AAD group updates"
                    |> Toast.success
                | Error (e: exn) ->
                    Toast.toast "Applying AAD group updates failed" e.Message
                    |> Toast.error

            let applyGroupUpdates groupUpdates =
                AsyncRx.defer (fun () ->
                    AsyncRx.ofPromise (promise {
                        let url = sprintf "/api/aad/apply-group-updates"
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        let data = (List.map Shared.AADGroups.GroupUpdate.encode >> Encode.list) groupUpdates
                        let! response = Fetch.post(url, data, Decode.nil (), requestProperties)
                        return ()
                    })
                    |> AsyncRx.map Ok
                    |> AsyncRx.catch (Error >> AsyncRx.single)
                )

            yield
                msgs
                |> AsyncRx.withLatestFrom states
                |> AsyncRx.choose (function
                    | (ApplyGroupUpdates, { GroupUpdates = LoadedGroupUpdates (_, groupUpdates) }) ->
                        Some (applyGroupUpdates (List.choose GroupUpdate.toDto groupUpdates))
                    | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showToast applyGroupUpdatesResponseToast
                |> AsyncRx.map ApplyGroupUpdatesResponse
        ]
        |> AsyncRx.mergeSeq
    )
