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
open System
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
            | CreateUser ({ Type = Teacher } as user) ->
                let (UserName userName) = user.Name
                $"%s{user.LastName.ToUpper()} %s{user.FirstName} (%s{userName})"
            | CreateUser ({ Type = Student _ } as user) ->
                let (UserName userName) = user.Name
                $"%s{user.LastName.ToUpper()} %s{user.FirstName} (%s{userName})"
            | UpdateUser ({ Type = Teacher } as user, ChangeUserName (UserName newUserName, newFirstName, newLastName, _)) ->
                let (UserName oldUserName) = user.Name
                $"%s{user.LastName.ToUpper()} %s{user.FirstName} (%s{oldUserName}) -> %s{newLastName.ToUpper()} %s{newFirstName} (%s{newUserName})"
            | UpdateUser ({ Type = Student (ClassName.ClassName className) } as user, ChangeUserName (UserName newUserName, newFirstName, newLastName, _)) ->
                let (UserName oldUserName) = user.Name
                $"%s{className}: %s{user.LastName.ToUpper()} %s{user.FirstName} (%s{oldUserName}) -> %s{newLastName.ToUpper()} %s{newFirstName} (%s{newUserName})"
            | UpdateUser ({ Type = Teacher } as user, SetSokratesId (SokratesId sokratesId)) ->
                let (UserName userName) = user.Name
                $"%s{user.LastName.ToUpper()} %s{user.FirstName} (%s{userName}): %s{sokratesId}"
            | UpdateUser ({ Type = Student (ClassName.ClassName className) } as user, SetSokratesId (SokratesId sokratesId)) ->
                $"%s{user.LastName.ToUpper()} %s{user.FirstName} (%s{className}): %s{sokratesId}"
            | UpdateUser ({ Type = Student (ClassName.ClassName oldClassName) } as user, MoveStudentToClass (ClassName.ClassName newClassName)) ->
                $"%s{user.LastName.ToUpper()} %s{user.FirstName}: %s{oldClassName} -> %s{newClassName}"
            | UpdateUser ({ Type = Teacher }, MoveStudentToClass _) ->
                "<invalid>"
            | DeleteUser ({ Type = Teacher } as user) ->
                let (UserName userName) = user.Name
                $"%s{user.LastName.ToUpper()} %s{user.FirstName} (%s{userName})"
            | DeleteUser ({ Type = Student _ } as user) ->
                $"%s{user.LastName.ToUpper()} %s{user.FirstName}"
            | CreateGroup Teacher ->
                "Teachers"
            | CreateGroup (Student (ClassName.ClassName className)) ->
                className
            | UpdateStudentClass (ClassName.ClassName oldClassName, ChangeStudentClassName (ClassName.ClassName newClassName)) ->
                sprintf "%s -> %s" oldClassName newClassName
            | DeleteGroup Teacher ->
                "Teachers"
            | DeleteGroup (Student (ClassName.ClassName className)) ->
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
            | CreateGroup _ ->
                "Create user group", Create
            | CreateUser { Type = Teacher } ->
                "Create teacher", Create
            | CreateUser { Type = Student (ClassName.ClassName className) } ->
                $"Create student of %s{className}", Create
            | UpdateUser ({ Type = Teacher }, ChangeUserName _) ->
                "Rename teacher", Update
            | UpdateUser (_, SetSokratesId _) ->
                "Set Sokrates ID", Update
            | UpdateUser ({ Type = Student _ }, ChangeUserName _) ->
                "Rename student", Update
            | UpdateUser (_, MoveStudentToClass (ClassName.ClassName className)) ->
                $"Move student to %s{className}", Update
            | DeleteUser { Type = Teacher } ->
                "Delete teacher", Delete
            | DeleteUser { Type = Student (ClassName.ClassName className) } ->
                $"Delete student of %s{className}", Delete
            | UpdateStudentClass (_, ChangeStudentClassName _) ->
                "Rename class", Update
            | DeleteGroup _ ->
                "Delete user group", Delete
        )
        |> List.map (fun ((title, kind), modifications) ->
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

type ApplyingModificationState = {
    ToApply: DirectoryModification list
    Applied: (DirectoryModification * Result<unit, string>) list
}

type ModificationsState =
    | Drafting
    | Applying of ApplyingModificationState
    | Applied of (DirectoryModification * Result<unit, string>) list
module ModificationsState =
    let isDrafting = function | Drafting -> true | _ -> false
    let isApplying = function | Applying _ -> true | _ -> false
    let isApplied = function | Applied _ -> true | _ -> false
    let tryGetApplicationResults = function
        | Drafting -> (None, [])
        | Applying { Applied = applied; ToApply = toApply } -> (List.tryHead toApply, applied)
        | Applied applied -> (None, applied)

type ModificationState =
    | ApplyingModification
    | AppliedModification of Result<unit, string>
module ModificationState =
    let tryGet modification modificationsState =
        let isApplying =
            modificationsState
            |> ModificationsState.tryGetApplicationResults
            |> fst
            |> fun v -> v = Some modification
        if isApplying then Some ApplyingModification
        else
            modificationsState
            |> ModificationsState.tryGetApplicationResults
            |> snd
            |> List.tryFind (fst >> (=) modification)
            |> Option.map (snd >> AppliedModification)

type LoadableDirectoryModifications =
    | LoadingModifications
    | LoadedModifications of DirectoryModificationGroup list
    | FailedToLoadModifications
module LoadableDirectoryModifications =
    let isLoading = function | LoadingModifications -> true | _ -> false
    let isLoaded = function | LoadedModifications _ -> true | _ -> false
    let isFailed = function | FailedToLoadModifications -> true | _ -> false

type Model = {
    ModificationsState: ModificationsState
    Modifications: LoadableDirectoryModifications
    Timestamp: DateTime
}

type Msg =
    | LoadModifications
    | LoadModificationsResponse of Result<DirectoryModification list, exn>
    | SelectAllModifications of bool
    | ToggleEnableModificationGroup of DirectoryModificationGroup
    | ToggleEnableModification of DirectoryModificationGroup * UIDirectoryModification
    | SetTimestamp of DateTime
    | ApplyModifications
    | ApplyModificationsResponse of Result<unit, string>

let rec update msg (model: Model) =
    let updateDirectoryModificationGroups isMatch fn =
        { model with
            Modifications =
                match model.ModificationsState, model.Modifications with
                | Drafting, LoadedModifications directoryModificationGroups ->
                    directoryModificationGroups
                    |> List.map (fun p -> if isMatch p then fn p else p)
                    |> LoadedModifications
                | _ -> model.Modifications
        }

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
    | LoadModifications -> { model with Modifications = LoadingModifications }
    | LoadModificationsResponse (Ok directoryModifications) ->
        { model with ModificationsState = Drafting; Modifications = LoadedModifications (DirectoryModificationGroup.fromDtoList directoryModifications) }
    | LoadModificationsResponse (Error _ex) ->
        { model with Modifications = FailedToLoadModifications }
    | SelectAllModifications value ->
        updateDirectoryModificationGroups (fun _ -> true) (fun p -> { p with IsEnabled = value })
    | ToggleEnableModificationGroup directoryModificationGroup ->
        updateDirectoryModificationGroup directoryModificationGroup (fun p -> { p with IsEnabled = not p.IsEnabled })
    | ToggleEnableModification (directoryModificationGroup, directoryModification) ->
        updateDirectoryModification directoryModificationGroup directoryModification (fun p -> { p with IsEnabled = not p.IsEnabled })
    | ApplyModifications ->
        match model.ModificationsState, model.Modifications with
        | Drafting, LoadedModifications directoryModificationGroups ->
            let toApply = directoryModificationGroups |> List.collect DirectoryModificationGroup.toDtoList
            if toApply.Length > 0 then { model with ModificationsState = Applying { ToApply = toApply; Applied = [] } }
            else model
        | Drafting, _
        | Applying _, _
        | Applied _, _ -> model
    | SetTimestamp timestamp -> { model with Timestamp = timestamp }
    | ApplyModificationsResponse applyResult ->
        match model.ModificationsState with
        | Applying { ToApply = modification :: rest; Applied = applied } ->
            let applied = (modification, applyResult) :: applied
            let modificationsState = if rest.Length > 0 then Applying { ToApply = rest; Applied = applied } else Applied applied
            { model with ModificationsState = modificationsState }
        | Applying { ToApply = [] } -> model
        | Drafting
        | Applied _ -> model

let init =
    {
        ModificationsState = Drafting
        Modifications = LoadingModifications
        Timestamp = DateTime.Today
    }

let view model dispatch =
    let isLocked = ModificationsState.isApplying model.ModificationsState || ModificationsState.isApplied model.ModificationsState

    let bulkOperations =
        Button.list [] [
            Button.button
                [
                    Button.Disabled isLocked
                    Button.OnClick (fun _e -> dispatch (SelectAllModifications true))
                ]
                [
                    Icon.icon [] [ Fa.i [ Fa.Solid.CheckSquare ] [] ]
                    span [] [ str "Select all modifications" ]
                ]
            Button.button
                [
                    Button.Disabled isLocked
                    Button.OnClick (fun _e -> dispatch (SelectAllModifications false))
                ]
                [
                    Icon.icon [] [ Fa.i [ Fa.Regular.CheckSquare ] [] ]
                    span [] [ str "Unselect all modifications" ]
                ]
        ]

    let directoryModificationGroupHeading directoryModificationGroup =
        let (icon, color) =
            match directoryModificationGroup.Kind with
            | Create -> Fa.Solid.Plus, IsSuccess
            | Update -> Fa.Solid.Sync, IsWarning
            | Delete -> Fa.Solid.Minus, IsDanger

        Panel.heading [] [
            Button.button
                [
                    Button.Disabled isLocked
                    Button.Size IsSmall
                    Button.Color (if directoryModificationGroup.IsEnabled then color else NoColor)
                    Button.Props [ Style [ MarginRight "0.5rem" ] ]
                    Button.OnClick (fun _ -> dispatch (ToggleEnableModificationGroup directoryModificationGroup))
                ]
                [ Fa.i [ icon ] [] ]
            span [] [ str directoryModificationGroup.Title ]
        ]

    let directoryModificationView directoryModificationGroup (directoryModification: UIDirectoryModification) =
        let (icon, color) =
            match directoryModification.Type with
            | CreateUser _
            | CreateGroup _ -> Fa.Solid.Plus, IsSuccess
            | UpdateUser _
            | UpdateStudentClass _ -> Fa.Solid.Sync, IsWarning
            | DeleteUser _
            | DeleteGroup _ -> Fa.Solid.Minus, IsDanger

        Panel.Block.div [] [
            Control.div [] [
                Level.level [] [
                    Level.left [] [
                        Button.button
                            [
                                Button.Disabled (isLocked || not directoryModificationGroup.IsEnabled)
                                Button.Size IsSmall
                                Button.Color (if directoryModificationGroup.IsEnabled && directoryModification.IsEnabled then color else NoColor)
                                Button.Props [ Style [ MarginRight "0.5rem" ] ]
                                Button.OnClick (fun _ -> dispatch (ToggleEnableModification (directoryModificationGroup, directoryModification)))
                            ]
                            [ Fa.i [ icon ] [] ]
                        str directoryModification.Description
                    ]
                    Level.right [] [
                        match ModificationState.tryGet directoryModification.Type model.ModificationsState with
                        | Some ApplyingModification -> Fa.i [ Fa.Solid.Spinner; Fa.Spin; Fa.CustomClass "has-text-info" ] []
                        | Some (AppliedModification (Ok ())) -> Fa.i [ Fa.Solid.Check; Fa.CustomClass "has-text-success" ] []
                        | Some (AppliedModification (Error _)) -> Fa.i [ Fa.CustomClass "fa-solid fa-xmark has-text-danger" ] []
                        | None -> ()
                    ]
                ]
                match ModificationState.tryGet directoryModification.Type model.ModificationsState with
                | Some ApplyingModification
                | Some (AppliedModification (Ok ())) -> ()
                | Some (AppliedModification (Error message)) -> Content.content [ Content.Modifiers [ Modifier.TextColor IsDanger ]; Content.Props [ Style [ WhiteSpace WhiteSpaceOptions.PreWrap ] ] ] [ str message ]
                | None -> ()
            ]
        ]

    let directoryModificationGroupView directoryModificationGroup =
        Panel.panel [] [
            directoryModificationGroupHeading directoryModificationGroup
            yield! List.map (directoryModificationView directoryModificationGroup) directoryModificationGroup.Modifications
        ]

    let modificationsView =
        match model.Modifications with
        | LoadingModifications ->
            Section.section [] [
                Progress.progress [ Progress.Color IsDanger ] []
            ]
        | FailedToLoadModifications ->
            Section.section [] [ Views.errorWithRetryButton "Error while loading modifications" (fun () -> dispatch LoadModifications) ]
        | LoadedModifications directoryModificationGroups ->
            Section.section [] [
                bulkOperations
                yield!
                    directoryModificationGroups
                    |> List.map directoryModificationGroupView
                    |> List.intersperse (Divider.divider [])
            ]

    let controls =
        Section.section [] [
            Field.div [ Field.HasAddons ] [
                Control.div [] [
                    Input.date [
                        Input.Value (model.Timestamp.ToString("yyyy-MM-dd"))
                        Input.OnChange (fun e -> dispatch (SetTimestamp (DateTime.Parse e.Value)))
                    ]
                ]
                Control.div [] [
                    Button.button
                        [
                            Button.Color IsInfo
                            Button.OnClick (fun _e -> dispatch LoadModifications)
                        ]
                        [
                            Icon.icon [] [ Fa.i [ Fa.Solid.Sync ] [] ]
                            span [] [ str "Reload modifications" ]
                        ]
                ]
            ]
            Button.list [] [
                Button.button
                    [
                        Button.Disabled (ModificationsState.isApplied model.ModificationsState || LoadableDirectoryModifications.isLoading model.Modifications)
                        Button.IsLoading (ModificationsState.isApplying model.ModificationsState)
                        Button.Color IsSuccess
                        Button.OnClick (fun _e -> dispatch ApplyModifications)
                    ]
                    [
                        Icon.icon [] [ Fa.i [ Fa.Solid.Save ] [] ]
                        span [] [ str "Apply modifications" ]
                    ]
            ]
        ]

    Container.container [] [
        modificationsView
        controls
    ]

let stream getAuthRequestHeader (pageActive: IAsyncObservable<bool>) (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
    pageActive
    |> AsyncRx.flatMapLatest (function
        | true ->
            [
                msgs

                let loadUpdates (timestamp: DateTime) =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync (async {
                            let url = sprintf "/api/ad/updates?date=%s" (timestamp.ToString("yyyy-MM-dd"))
                            let! authHeader = getAuthRequestHeader ()
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            let coders = Extra.empty |> Thoth.addCoders
                            let! (modifications: DirectoryModification list) = Fetch.get(url, properties = requestProperties, extra = coders) |> Async.AwaitPromise
                            return modifications
                        })
                        |> AsyncRx.map Ok
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )

                msgs
                |> AsyncRx.choose (function | LoadModifications -> Some () | _ -> None)
                |> AsyncRx.withLatestFrom (states |> AsyncRx.map (snd >> (fun state -> state.Timestamp)))
                |> AsyncRx.map snd
                |> AsyncRx.startWith [ init.Timestamp ]
                |> AsyncRx.flatMapLatest loadUpdates
                |> AsyncRx.showSimpleErrorToast (fun e -> "Loading AD modifications failed", e.Message)
                |> AsyncRx.map LoadModificationsResponse

                let applyModifications (modifications: DirectoryModification list) =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync (async {
                            let url = "/api/ad/updates/apply"
                            let! authHeader = getAuthRequestHeader ()
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            let coders = Extra.empty |> Thoth.addCoders
                            match! Fetch.tryPost(url, modifications, properties = requestProperties, extra = coders) |> Async.AwaitPromise with
                            | Ok () -> return Ok ()
                            | Error (FetchFailed response) ->
                                let! message = response.json<string>() |> Async.AwaitPromise
                                return Error message
                            | Error v -> return Error $"Unexpected error: %A{v}"
                        })
                    )

                states
                |> AsyncRx.choose (function
                    | Some ApplyModifications, { ModificationsState = Applying modifications }
                    | Some (ApplyModificationsResponse _), { ModificationsState = Applying modifications } ->
                        modifications.ToApply
                        |> List.take 1
                        |> applyModifications
                        |> Some
                    | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.map ApplyModificationsResponse

                // TODO show summary after applying modifications

            ]
            |> AsyncRx.mergeSeq
        | false -> AsyncRx.empty ()
    )
