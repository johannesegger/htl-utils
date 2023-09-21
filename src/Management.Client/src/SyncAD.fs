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

type DirectoryModificationState =
    | IgnoredModification
    | StagedModification
    | CommittingModification
    | CommittedModification of Result<unit, string>

type UIDirectoryModification = {
    Description: string
    State: DirectoryModificationState
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
            Description = description
            State = StagedModification
            Type = modification
        }

type DirectoryModificationKind =
    | Create
    | Update
    | Delete

type DirectoryModificationGroup =
    {
        Title: string
        Kind: DirectoryModificationKind
        Modifications: UIDirectoryModification list
    } with
    member v.HasIgnoredModification =
        v.Modifications
        |> List.exists (fun v -> v.State = IgnoredModification)
    member v.HasStagedModification =
        v.Modifications
        |> List.exists (fun v -> v.State = StagedModification)

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
                Title = title
                Kind = kind
                Modifications =
                    modifications
                    |> List.map UIDirectoryModification.fromDto
                    |> List.sortBy (fun v -> v.Description)
            }
        )

type LoadableDirectoryModifications =
    | LoadingModifications
    | LoadedModifications of DirectoryModificationGroup list
    | FailedToLoadModifications
module LoadableDirectoryModifications =
    let isLoading = function | LoadingModifications -> true | _ -> false
    let isLoaded = function | LoadedModifications _ -> true | _ -> false
    let isFailed = function | FailedToLoadModifications -> true | _ -> false

type Model =
    {
        Modifications: LoadableDirectoryModifications
        Timestamp: DateTime
        ApplyPaused: bool
    } with
    member v.IsCommitting =
        match v.Modifications with
        | LoadedModifications directoryModificationGroups ->
            directoryModificationGroups
            |> List.collect (fun group -> group.Modifications)
            |> List.exists (fun modification -> modification.State = CommittingModification)
        | LoadingModifications
        | FailedToLoadModifications -> false

type Msg =
    | LoadModifications
    | LoadModificationsResponse of Result<DirectoryModification list, exn>
    | SelectAllModifications of bool
    | ToggleEnableModificationGroup of DirectoryModificationGroup
    | ToggleEnableModification of DirectoryModificationGroup * UIDirectoryModification
    | SetTimestamp of DateTime
    | ApplyModifications
    | ApplyModificationsResponse of Result<unit, string>
    | CancelApplyModifications

let rec update msg (model: Model) =
    let updateDirectoryModificationGroups isMatch fn =
        { model with
            Modifications =
                match model.Modifications with
                | LoadedModifications directoryModificationGroups ->
                    directoryModificationGroups
                    |> List.map (fun p -> if isMatch p then fn p else p)
                    |> LoadedModifications
                | _ -> model.Modifications
        }

    let updateDirectoryModifications isGroupMatch isModificationMatch fn =
        updateDirectoryModificationGroups isGroupMatch (fun group ->
            { group with
                Modifications =
                    group.Modifications
                    |> List.map (fun modification ->
                        if isModificationMatch modification then fn modification
                        else modification
                    )
            }
        )

    let updateDirectoryModificationGroup directoryModificationGroup fn =
        updateDirectoryModificationGroups ((=) directoryModificationGroup) fn

    let updateDirectoryModification directoryModificationGroup directoryModification fn =
        updateDirectoryModifications ((=) directoryModificationGroup) ((=) directoryModification) fn

    let updateFirst filter update (directoryModificationGroups: DirectoryModificationGroup list) =
        let mutable foundFirst = false
        directoryModificationGroups
        |> List.map (fun group ->
            let modifications =
                group.Modifications
                |> List.map (fun modification ->
                    if not foundFirst && filter modification then
                        foundFirst <- true
                        update modification
                    else modification
                )
            { group with Modifications = modifications }
        )

    let setNextModificationAsCommitting =
        updateFirst
            (fun v -> v.State = StagedModification)
            (fun v -> { v with State = CommittingModification })

    let setCommittingModificationResult applyResult =
        updateFirst
            (fun v -> v.State = CommittingModification)
            (fun v -> { v with State = CommittedModification applyResult })

    match msg with
    | LoadModifications -> { model with Modifications = LoadingModifications }
    | LoadModificationsResponse (Ok directoryModifications) ->
        { model with Modifications = LoadedModifications (DirectoryModificationGroup.fromDtoList directoryModifications) }
    | LoadModificationsResponse (Error _ex) ->
        { model with Modifications = FailedToLoadModifications }
    | SelectAllModifications value ->
        let (oldState, newState) =
            if value then (IgnoredModification, StagedModification)
            else (StagedModification, IgnoredModification)
        updateDirectoryModifications
            (fun _ -> true)
            (fun modification -> modification.State = oldState)
            (fun p -> { p with State = newState })
    | ToggleEnableModificationGroup directoryModificationGroup ->
        let (oldState, newState) =
            if directoryModificationGroup.HasIgnoredModification then (IgnoredModification, StagedModification)
            else (StagedModification, IgnoredModification)
        updateDirectoryModifications
            ((=) directoryModificationGroup)
            (fun modification -> modification.State = oldState)
            (fun p -> { p with State = newState })
    | ToggleEnableModification (directoryModificationGroup, directoryModification) ->
        let modificationState =
            match directoryModification.State with
            | IgnoredModification -> Some StagedModification
            | StagedModification -> Some IgnoredModification
            | CommittingModification
            | CommittedModification _ -> None
        match modificationState with
        | Some newState ->
            updateDirectoryModification directoryModificationGroup directoryModification (fun p -> { p with State = newState })
        | None -> model
    | SetTimestamp timestamp -> { model with Timestamp = timestamp }
    | ApplyModifications ->
        match model.Modifications with
        | LoadedModifications directoryModificationGroups ->
            let directoryModificationGroups = setNextModificationAsCommitting directoryModificationGroups
            { model with Modifications = LoadedModifications directoryModificationGroups; ApplyPaused = false }
        | LoadingModifications
        | FailedToLoadModifications -> model
    | ApplyModificationsResponse applyResult ->
        match model.Modifications with
        | LoadedModifications directoryModificationGroups ->
            let directoryModificationGroups =
                directoryModificationGroups
                |> setCommittingModificationResult applyResult
                |> fun v ->
                    if model.ApplyPaused then v
                    else setNextModificationAsCommitting v
            { model with Modifications = LoadedModifications directoryModificationGroups }
        | LoadingModifications
        | FailedToLoadModifications -> model
    | CancelApplyModifications ->
        { model with ApplyPaused = true }

let init =
    {
        Modifications = LoadingModifications
        Timestamp = DateTime.Today
        ApplyPaused = false
    }

let view model dispatch =
    let bulkOperations =
        Button.list [] [
            Button.button
                [
                    Button.OnClick (fun _e -> dispatch (SelectAllModifications true))
                ]
                [
                    Icon.icon [] [ Fa.i [ Fa.Solid.CheckSquare ] [] ]
                    span [] [ str "Select all modifications" ]
                ]
            Button.button
                [
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
                    Button.Disabled (not directoryModificationGroup.HasIgnoredModification && not directoryModificationGroup.HasStagedModification)
                    Button.Size IsSmall
                    Button.Color (if not directoryModificationGroup.HasIgnoredModification then color else NoColor)
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
                                Button.Disabled (
                                    match directoryModification.State with
                                    | IgnoredModification | StagedModification -> false
                                    | CommittingModification | CommittedModification _ -> true
                                )
                                Button.Size IsSmall
                                Button.Color (if directoryModification.State <> IgnoredModification then color else NoColor)
                                Button.Props [ Style [ MarginRight "0.5rem" ] ]
                                Button.OnClick (fun _ -> dispatch (ToggleEnableModification (directoryModificationGroup, directoryModification)))
                            ]
                            [ Fa.i [ icon ] [] ]
                        str directoryModification.Description
                    ]
                    Level.right [] [
                        match directoryModification.State with
                        | IgnoredModification
                        | StagedModification -> ()
                        | CommittingModification -> Fa.i [ Fa.Solid.Spinner; Fa.Spin; Fa.CustomClass "has-text-info" ] []
                        | CommittedModification (Ok ()) -> Fa.i [ Fa.Solid.Check; Fa.CustomClass "has-text-success" ] []
                        | CommittedModification (Error _) -> Fa.i [ Fa.CustomClass "fa-solid fa-xmark has-text-danger" ] []
                    ]
                ]
                match directoryModification.State with
                | IgnoredModification
                | StagedModification
                | CommittingModification
                | CommittedModification (Ok ()) -> ()
                | CommittedModification (Error message) -> Content.content [ Content.Modifiers [ Modifier.TextColor IsDanger ]; Content.Props [ Style [ WhiteSpace WhiteSpaceOptions.PreWrap ] ] ] [ str message ]
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
                        Input.Disabled model.IsCommitting
                        Input.Value (model.Timestamp.ToString("yyyy-MM-dd"))
                        Input.OnChange (fun e -> dispatch (SetTimestamp (DateTime.Parse e.Value)))
                    ]
                ]
                Control.div [] [
                    Button.button
                        [
                            Button.Disabled model.IsCommitting
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
                if model.IsCommitting then
                    Button.button
                        [
                            Button.Color IsDanger
                            Button.OnClick (fun _e -> dispatch CancelApplyModifications)
                        ]
                        [
                            Icon.icon [] [ Fa.i [ Fa.Solid.Save ] [] ]
                            span [] [ str "Stop applying modifications" ]
                        ]
                else
                    Button.button
                        [
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
                    | Some ApplyModifications, { Modifications = LoadedModifications directoryModificationGroups }
                    | Some (ApplyModificationsResponse _), { Modifications = LoadedModifications directoryModificationGroups } ->
                        directoryModificationGroups
                        |> List.collect (fun v -> v.Modifications)
                        |> List.tryFind (fun v -> v.State = CommittingModification)
                        |> Option.map (fun v -> applyModifications [ v.Type ])
                    | _ -> None
                )
                |> AsyncRx.switchLatest
                |> AsyncRx.map ApplyModificationsResponse

                // TODO show summary after applying modifications

            ]
            |> AsyncRx.mergeSeq
        | false -> AsyncRx.empty ()
    )
