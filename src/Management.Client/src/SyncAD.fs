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
            | CreateUser ({ Type = Teacher } as user, _, _) ->
                let (UserName userName) = user.Name
                sprintf "%s %s (%s)" (user.LastName.ToUpper()) user.FirstName userName
            | CreateUser ({ Type = Student _ } as user, _, _) ->
                let (UserName userName) = user.Name
                sprintf "%s %s (%s)" (user.LastName.ToUpper()) user.FirstName userName
            | UpdateUser ({ Type = Teacher } as user, ChangeUserName (UserName newUserName, newFirstName, newLastName, _)) ->
                let (UserName oldUserName) = user.Name
                sprintf "%s %s (%s) -> %s %s (%s)" (user.LastName.ToUpper()) user.FirstName oldUserName (newLastName.ToUpper()) newFirstName newUserName
            | UpdateUser ({ Type = Student (ClassName.ClassName className) } as user, ChangeUserName (UserName newUserName, newFirstName, newLastName, _)) ->
                let (UserName oldUserName) = user.Name
                sprintf "%s: %s %s (%s) -> %s %s (%s)" className (user.LastName.ToUpper()) user.FirstName oldUserName (newLastName.ToUpper()) newFirstName newUserName
            | UpdateUser ({ Type = Teacher } as user, SetSokratesId (SokratesId sokratesId)) ->
                let (UserName userName) = user.Name
                sprintf "%s %s (%s): %s" (user.LastName.ToUpper()) user.FirstName userName sokratesId
            | UpdateUser ({ Type = Student (ClassName.ClassName className) } as user, SetSokratesId (SokratesId sokratesId)) ->
                let (UserName userName) = user.Name
                sprintf "%s %s (%s): %s" (user.LastName.ToUpper()) user.FirstName className sokratesId
            | UpdateUser ({ Type = Student (ClassName.ClassName oldClassName) } as user, MoveStudentToClass (ClassName.ClassName newClassName)) ->
                sprintf "%s %s: %s -> %s" (user.LastName.ToUpper()) user.FirstName oldClassName newClassName
            | UpdateUser ({ Type = Teacher }, MoveStudentToClass _) ->
                "<invalid>"
            | DeleteUser ({ Type = Teacher } as user) ->
                let (UserName userName) = user.Name
                sprintf "%s %s (%s)" (user.LastName.ToUpper()) user.FirstName userName
            | DeleteUser ({ Type = Student _ } as user) ->
                sprintf "%s %s" (user.LastName.ToUpper()) user.FirstName
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
                "01-CreateGroup", "Create user group", Create
            | CreateUser ({ Type = Teacher }, _, _) ->
                "02-CreateTeacher", "Create teacher", Create
            | CreateUser ({ Type = Student (ClassName.ClassName className) }, _, _) ->
                sprintf "03-CreateStudent-%s" className, sprintf "Create student of %s" className, Create
            | UpdateUser ({ Type = Teacher }, ChangeUserName _) ->
                "04-RenameTeacher", "Rename teacher", Update
            | UpdateUser (_, SetSokratesId _) ->
                "05-SetSokratesId", "Set Sokrates ID", Update
            | UpdateUser ({ Type = Student _ }, ChangeUserName _) ->
                "06-RenameStudent", "Rename student", Update
            | UpdateUser (_, MoveStudentToClass (ClassName.ClassName className)) ->
                sprintf "07-MoveStudentToClass-%s" className, sprintf "Move student to %s" className, Update
            | DeleteUser ({ Type = Teacher }) ->
                "08-DeleteTeacher", "Delete teacher", Delete
            | DeleteUser ({ Type = Student (ClassName.ClassName className) }) ->
                sprintf "09-DeleteStudent-%s" className, sprintf "Delete student of %s" className, Delete
            | UpdateStudentClass (_, ChangeStudentClassName _) ->
                "10-RenameClass", "Rename class", Update
            | DeleteGroup _ ->
                "11-DeleteGroup", "Delete user group", Delete
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
    | Applying of DirectoryModification list
    | Applied
module ModificationsState =
    let isDrafting = function | Drafting -> true | _ -> false
    let isApplying = function | Applying _ -> true | _ -> false
    let isApplied = function | Applied -> true | _ -> false

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
    | ApplyModificationsResponse of Result<unit, exn>

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
    | LoadModificationsResponse (Error ex) ->
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
            { model with ModificationsState = Applying (directoryModificationGroups |> List.collect DirectoryModificationGroup.toDtoList) }
        | Drafting, _
        | Applying _, _
        | Applied, _ -> model
    | SetTimestamp timestamp -> { model with Timestamp = timestamp }
    | ApplyModificationsResponse (Ok ())
    | ApplyModificationsResponse (Error _) -> { model with ModificationsState = Applied }

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
            Panel.icon [] []
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
                            Button.OnClick (fun e -> dispatch LoadModifications)
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
                        Button.OnClick (fun _ -> dispatch ApplyModifications)
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
                            let url = sprintf "/api/ad/updates/apply"
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
                    | Some ApplyModifications, { ModificationsState = Applying modifications } -> Some (applyModifications modifications)
                    | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showSimpleErrorToast (fun e -> "Applying AD modifications failed", e.Message)
                |> AsyncRx.showSimpleSuccessToast (fun () -> "Applying AD modifications", "Successfully applied AD modifications")
                |> AsyncRx.map ApplyModificationsResponse
            ]
            |> AsyncRx.mergeSeq
        | false -> AsyncRx.empty ()
    )
