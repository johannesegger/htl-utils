module SyncAD

open ADModifications.DataTransferTypes
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

type DirectoryModificationKind =
    | Create
    | Update
    | Delete

type UIDirectoryModification = {
    IsEnabled: bool
    Description: string
    Type: DirectoryModification
}

module UIDirectoryModification =
    let fromDto modification =
        let description =
            match modification with
            | CreateUser ({ Type = Teacher } as user, _) ->
                let (UserName userName) = user.Name
                sprintf "%s %s (%s)" (user.LastName.ToUpper()) user.FirstName userName
            | CreateUser ({ Type = Student _ } as user, _) ->
                sprintf "%s %s" (user.LastName.ToUpper()) user.FirstName
            | UpdateUser ({ Type = Teacher } as user, ChangeUserName (UserName newUserName, newFirstName, newLastName)) ->
                let (UserName oldUserName) = user.Name
                sprintf "%s %s (%s) -> %s %s (%s)" (user.LastName.ToUpper()) user.FirstName oldUserName (newLastName.ToUpper()) newFirstName newUserName
            | UpdateUser ({ Type = Student (GroupName className) } as user, ChangeUserName (UserName newUserName, newFirstName, newLastName)) ->
                let (UserName oldUserName) = user.Name
                sprintf "%s: %s %s (%s) -> %s %s (%s)" className (user.LastName.ToUpper()) user.FirstName oldUserName (newLastName.ToUpper()) newFirstName newUserName
            | UpdateUser ({ Type = Student (GroupName oldClassName) } as user, MoveStudentToClass (GroupName newClassName)) ->
                sprintf "%s %s: %s -> %s" (user.LastName.ToUpper()) user.FirstName oldClassName newClassName
            | UpdateUser ({ Type = Teacher }, MoveStudentToClass _) ->
                "<invalid>"
            | DeleteUser ({ Type = Teacher } as user) ->
                let (UserName userName) = user.Name
                sprintf "%s %s (%s)" (user.LastName.ToUpper()) user.FirstName userName
            | DeleteUser ({ Type = Student _ } as user) ->
                sprintf "%s %s" (user.LastName.ToUpper()) user.FirstName
            | CreateGroup (Teacher, _) ->
                "Teachers"
            | CreateGroup (Student (GroupName className), _) ->
                className
            | UpdateGroup (Teacher, ChangeGroupName (GroupName newGroupName)) ->
                sprintf "Teachers -> %s" newGroupName
            | UpdateGroup (Student (GroupName oldClassName), ChangeGroupName (GroupName newClassName)) ->
                sprintf "%s -> %s" oldClassName newClassName
            | DeleteGroup Teacher ->
                "Teachers"
            | DeleteGroup (Student (GroupName className)) ->
                className
        {
            IsEnabled = true
            Description = description
            Type = modification
        }
    let toDto modification =
        if modification.IsEnabled then Some modification.Type
        else None

type DirectoryModificationGroup = {
    IsEnabled: bool
    Title: string
    Kind: DirectoryModificationKind
    Modifications: UIDirectoryModification list
}

module DirectoryModificationGroup =
    let fromDtoList modifications =
        modifications
        |> List.groupBy (function
            | CreateUser ({ Type = Teacher }, _) ->
                "01-CreateTeacher", "Create teacher", Create
            | CreateUser ({ Type = Student (GroupName className) }, _) ->
                sprintf "02-CreateStudent-%s" className, sprintf "Create student of %s" className, Create
            | UpdateUser ({ Type = Teacher }, ChangeUserName _) ->
                "03-RenameTeacher", "Rename teacher", Update
            | UpdateUser ({ Type = Student _ }, ChangeUserName _) ->
                "04-RenameStudent", "Rename student", Update
            | UpdateUser (_, MoveStudentToClass (GroupName className)) ->
                sprintf "05-MoveStudentToClass-%s" className, sprintf "Move student to %s" className, Update
            | DeleteUser ({ Type = Teacher }) ->
                "06-DeleteTeacher", "Delete teacher", Delete
            | DeleteUser ({ Type = Student (GroupName className) }) ->
                sprintf "07-DeleteStudent-%s" className, sprintf "Delete student of %s" className, Delete
            | CreateGroup _ ->
                "08-CreateGroup", "Create user group", Create
            | UpdateGroup (_, ChangeGroupName _) ->
                "09-RenameGroup", "Rename user group", Update
            | DeleteGroup _ ->
                "10-DeleteGroup", "Delete user group", Delete
        )
        |> List.sortBy (fun ((key, _, _), _) -> key)
        |> List.map (fun ((_, title, kind), modifications) ->
            {
                IsEnabled = true
                Title = title
                Kind = kind
                Modifications =
                    modifications
                    |> List.map UIDirectoryModification.fromDto
                    |> List.sortBy (fun v -> v.Description)
            }
        )
    let toDtoList directoryModificationGroup =
        if directoryModificationGroup.IsEnabled then
            List.choose UIDirectoryModification.toDto directoryModificationGroup.Modifications
        else []

type ModificationsState =
    | Drafting
    | Applying
    | Applied

type LoadableDirectoryModifications =
    | NotLoadedModifications
    | LoadingModifications
    | LoadedModifications of ModificationsState * DirectoryModificationGroup list
    | FailedToLoadModifications

type Model = LoadableDirectoryModifications

type Msg =
    | LoadModifications
    | LoadModificationsResponse of Result<DirectoryModification list, exn>
    | SelectAllModifications of bool
    | ToggleEnableModificationGroup of DirectoryModificationGroup
    | ToggleEnableModification of DirectoryModificationGroup * UIDirectoryModification
    | ApplyModifications
    | ApplyModificationsResponse of Result<unit, exn>

let rec update msg model =
    let updateDirectoryModificationGroups isMatch fn =
        match model with
        | LoadedModifications (Drafting, directoryModificationGroups) ->
            let directoryModificationGroups =
                directoryModificationGroups
                |> List.map (fun p -> if isMatch p then fn p else p)
            LoadedModifications (Drafting, directoryModificationGroups)
        | _ -> model

    let updateDirectoryModificationGroup directoryModificationGroup fn =
        updateDirectoryModificationGroups ((=) directoryModificationGroup) fn

    let updateDirectoryModification directoryModificationGroup directoryModification fn =
        updateDirectoryModificationGroup directoryModificationGroup (fun directoryModificationGroup ->
            let modifications =
                directoryModificationGroup.Modifications
                |> List.map (fun m -> if m = directoryModification then fn m else m)
            { directoryModificationGroup with Modifications = modifications }
        )

    match msg with
    | LoadModifications -> LoadingModifications
    | LoadModificationsResponse (Ok directoryModifications) ->
        LoadedModifications (Drafting, DirectoryModificationGroup.fromDtoList directoryModifications)
    | LoadModificationsResponse (Error ex) ->
        FailedToLoadModifications
    | SelectAllModifications value ->
        updateDirectoryModificationGroups (fun _ -> true) (fun p -> { p with IsEnabled = value })
    | ToggleEnableModificationGroup directoryModificationGroup ->
        updateDirectoryModificationGroup directoryModificationGroup (fun p -> { p with IsEnabled = not p.IsEnabled })
    | ToggleEnableModification (directoryModificationGroup, directoryModification) ->
        updateDirectoryModification directoryModificationGroup directoryModification (fun p -> { p with IsEnabled = not p.IsEnabled })
    | ApplyModifications ->
        match model with
        | LoadedModifications (Drafting, directoryModificationGroups) ->
            LoadedModifications (Applying, directoryModificationGroups)
        | LoadedModifications (Applying, _)
        | LoadedModifications (Applied, _)
        | NotLoadedModifications
        | LoadingModifications
        | FailedToLoadModifications -> model
    | ApplyModificationsResponse (Ok ())
    | ApplyModificationsResponse (Error _) ->
        match model with
        | LoadedModifications (Applying, directoryModificationGroups) ->
            LoadedModifications (Applied, directoryModificationGroups)
        | LoadedModifications (Drafting, _)
        | LoadedModifications (Applied, _)
        | NotLoadedModifications
        | LoadingModifications
        | FailedToLoadModifications -> model

let init = NotLoadedModifications

let view model dispatch =
    let bulkOperations isLocked =
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

    let directoryModificationGroupView isLocked directoryModificationGroup =
        let heading title icon color =
            Panel.heading [] [
                Button.button
                    [
                        Button.Disabled isLocked
                        Button.Size IsSmall
                        Button.Color (if directoryModificationGroup.IsEnabled then color else NoColor)
                        Button.Props [ Style [ MarginRight "0.5rem" ] ]
                        Button.OnClick (fun e -> dispatch (ToggleEnableModificationGroup directoryModificationGroup))
                    ]
                    [ Fa.i [ icon ] [] ]
                span [] [ str title ]
            ]

        let directoryModificationView icon color (directoryModification: UIDirectoryModification) =
            Panel.block [ ] [
                Panel.icon [] []
                Button.button
                    [
                        Button.Disabled (isLocked || not directoryModificationGroup.IsEnabled)
                        Button.Size IsSmall
                        Button.Color (if directoryModificationGroup.IsEnabled && directoryModification.IsEnabled then color else NoColor)
                        Button.Props [ Style [ MarginRight "0.5rem" ] ]
                        Button.OnClick (fun e -> dispatch (ToggleEnableModification (directoryModificationGroup, directoryModification)))
                    ]
                    [ Fa.i [ icon ] [] ]
                str directoryModification.Description
            ]

        let memberText i =
            if i = 1 then sprintf "%d member" i
            else sprintf "%d members" i

        let (icon, color) =
            match directoryModificationGroup.Kind with
            | Create -> Fa.Solid.Plus, IsSuccess
            | Update -> Fa.Solid.Sync, IsWarning
            | Delete -> Fa.Solid.Minus, IsDanger

        Panel.panel [ ] [
            heading directoryModificationGroup.Title icon color
            yield! List.map (directoryModificationView icon color) directoryModificationGroup.Modifications
        ]

    let modifications =
        match model with
        | NotLoadedModifications
        | LoadingModifications ->
            Section.section [] [
                Progress.progress [ Progress.Color IsDanger ] []
            ]
        | FailedToLoadModifications ->
            Section.section [] [ Views.errorWithRetryButton "Error while loading modifications" (fun () -> dispatch LoadModifications) ]
        | LoadedModifications (state, directoryModificationGroups) ->
            let isLocked =
                match state with
                | Drafting -> false
                | Applying
                | Applied -> true
            Section.section [] [
                bulkOperations isLocked
                yield!
                    directoryModificationGroups
                    |> List.map (directoryModificationGroupView isLocked)
                    |> List.intersperse (Divider.divider [])
            ]

    let buttons =
        match model with
        | NotLoadedModifications
        | LoadingModifications
        | FailedToLoadModifications -> None
        | LoadedModifications (state, _) ->
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
                                Button.OnClick (fun e -> dispatch LoadModifications)
                            ]
                            [
                                Icon.icon [] [ Fa.i [ Fa.Solid.Sync ] [] ]
                                span [] [ str "Reload AD modifications" ]
                            ]
                    ]
                    Control.div [] [
                        Button.button
                            [
                                Button.Disabled (match state with | Applied -> true | Drafting | Applying -> false)
                                Button.IsLoading (match state with | Applying -> true | Drafting | Applied -> false)
                                Button.Color IsSuccess
                                Button.OnClick (fun e -> dispatch ApplyModifications)
                            ]
                            [
                                Icon.icon [] [ Fa.i [ Fa.Solid.Save ] [] ]
                                span [] [ str "Apply modifications" ]
                            ]
                    ]
                ]
            ]
            |> Some

    Container.container [] [
        modifications
        yield! Option.toList buttons
    ]

let stream (getAuthRequestHeader, (pageActive: IAsyncObservable<bool>)) (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
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
                            let! modifications = Fetch.tryGet("/api/ad/updates", Decode.list DirectoryModification.decoder, requestProperties) |> Async.AwaitPromise
                            match modifications with
                            | Ok v -> return v
                            | Error e -> return failwith (String.ellipsis 200 e)
                        })
                        |> AsyncRx.map Ok
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )

                states
                |> AsyncRx.choose (fst >> function | Some LoadModifications -> Some loadUpdates | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showSimpleErrorToast (fun e -> "Loading AD modifications failed", e.Message)
                |> AsyncRx.map LoadModificationsResponse

                AsyncRx.single LoadModifications

                let applyModifications modifications =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync (async {
                            let url = sprintf "/api/ad/updates/apply"
                            let data = (List.map DirectoryModification.encode >> Encode.list) modifications
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
                    | (ApplyModifications, LoadedModifications (_, directoryModificationGroups)) ->
                        Some (applyModifications (List.collect DirectoryModificationGroup.toDtoList directoryModificationGroups))
                    | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showSimpleErrorToast (fun e -> "Applying AD modifications failed", e.Message)
                |> AsyncRx.showSimpleSuccessToast (fun () -> "Applying AD modifications", "Successfully applied AD modifications")
                |> AsyncRx.map ApplyModificationsResponse
            ]
            |> AsyncRx.mergeSeq
        | false -> AsyncRx.empty ()
    )
