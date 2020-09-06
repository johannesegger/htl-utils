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
            | UpdateUser ({ Type = Teacher } as user, SetSokratesId (SokratesId sokratesId)) ->
                let (UserName userName) = user.Name
                sprintf "%s %s (%s): %s" (user.LastName.ToUpper()) user.FirstName userName sokratesId
            | UpdateUser ({ Type = Student (GroupName className) } as user, SetSokratesId (SokratesId sokratesId)) ->
                let (UserName userName) = user.Name
                sprintf "%s %s (%s): %s" (user.LastName.ToUpper()) user.FirstName className sokratesId
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
            | UpdateUser (_, SetSokratesId _) ->
                "04-SetSokratesId", "Set Sokrates ID", Update
            | UpdateUser ({ Type = Student _ }, ChangeUserName _) ->
                "05-RenameStudent", "Rename student", Update
            | UpdateUser (_, MoveStudentToClass (GroupName className)) ->
                sprintf "06-MoveStudentToClass-%s" className, sprintf "Move student to %s" className, Update
            | DeleteUser ({ Type = Teacher }) ->
                "07-DeleteTeacher", "Delete teacher", Delete
            | DeleteUser ({ Type = Student (GroupName className) }) ->
                sprintf "08-DeleteStudent-%s" className, sprintf "Delete student of %s" className, Delete
            | CreateGroup _ ->
                "09-CreateGroup", "Create user group", Create
            | UpdateGroup (_, ChangeGroupName _) ->
                "10-RenameGroup", "Rename user group", Update
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

type ManualModificationDraft = {
    Modification: DirectoryModification
    ValidationErrors: string list
}

type Model = {
    ModificationsState: ModificationsState
    AutoModifications: LoadableDirectoryModifications
    ManualModifications: DirectoryModificationGroup
    ManualModificationDraft: ManualModificationDraft
}

type Msg =
    | LoadModifications
    | LoadModificationsResponse of Result<DirectoryModification list, exn>
    | SelectAllModifications of bool
    | ToggleEnableModificationGroup of DirectoryModificationGroup
    | ToggleEnableModification of DirectoryModificationGroup * UIDirectoryModification
    | SetManualModification of DirectoryModification
    | AddManualModification
    | ApplyModifications
    | ApplyModificationsResponse of Result<unit, exn>

let private validateManualModificationDraft directoryModification =
    {
        Modification = directoryModification
        ValidationErrors =
            [
                match directoryModification with
                | CreateUser ({ Name = UserName userName; Type = Teacher }, _) ->
                    if System.String.IsNullOrWhiteSpace userName then "User name must not be empty."
                | CreateUser ({ Name = UserName userName; Type = Student (GroupName className) }, _) ->
                    if System.String.IsNullOrWhiteSpace className then "Class name must not be empty."
                    if System.String.IsNullOrWhiteSpace userName then "User name must not be empty."
                | DeleteUser { Name = UserName userName } ->
                    if System.String.IsNullOrWhiteSpace userName then "User name must not be empty."
                | UpdateUser _
                | CreateGroup _
                | UpdateGroup _
                | DeleteGroup _ -> "Modification type not supported"
            ]
    }

let private clearManualModificationDraft manualModificationDraft =
    let clearedModification =
        match manualModificationDraft.Modification with
        | CreateUser (user, _) ->
            CreateUser ({ user with Name = UserName ""; FirstName = ""; LastName = "" }, "")
        | DeleteUser user ->
            DeleteUser { user with Name = UserName "" }
        | UpdateUser _
        | CreateGroup _
        | UpdateGroup _
        | DeleteGroup _ -> failwith "Not implemented"
    validateManualModificationDraft clearedModification

let rec update msg model =
    let updateDirectoryModificationGroups isMatch fn =
        { model with
            ManualModifications = if isMatch model.ManualModifications then fn model.ManualModifications else model.ManualModifications
            AutoModifications =
                match model.ModificationsState, model.AutoModifications with
                | Drafting, LoadedModifications directoryModificationGroups ->
                    directoryModificationGroups
                    |> List.map (fun p -> if isMatch p then fn p else p)
                    |> LoadedModifications
                | _ -> model.AutoModifications
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
    | LoadModifications -> { model with AutoModifications = LoadingModifications }
    | LoadModificationsResponse (Ok directoryModifications) ->
        { model with ModificationsState = Drafting; AutoModifications = LoadedModifications (DirectoryModificationGroup.fromDtoList directoryModifications) }
    | LoadModificationsResponse (Error ex) ->
        { model with AutoModifications = FailedToLoadModifications }
    | SelectAllModifications value ->
        updateDirectoryModificationGroups (fun _ -> true) (fun p -> { p with IsEnabled = value })
    | ToggleEnableModificationGroup directoryModificationGroup ->
        updateDirectoryModificationGroup directoryModificationGroup (fun p -> { p with IsEnabled = not p.IsEnabled })
    | ToggleEnableModification (directoryModificationGroup, directoryModification) ->
        updateDirectoryModification directoryModificationGroup directoryModification (fun p -> { p with IsEnabled = not p.IsEnabled })
    | ApplyModifications ->
        match model.ModificationsState, model.AutoModifications with
        | Drafting, LoadedModifications directoryModificationGroups ->
            { model with ModificationsState = Applying (directoryModificationGroups @ [ model.ManualModifications ] |> List.collect DirectoryModificationGroup.toDtoList) }
        | Drafting, _ -> { model with ModificationsState = Applying (DirectoryModificationGroup.toDtoList model.ManualModifications) }
        | Drafting, _
        | Applying _, _
        | Applied, _ -> model
    | SetManualModification directoryModification ->
        { model with ManualModificationDraft = validateManualModificationDraft directoryModification }
    | AddManualModification ->
        if List.isEmpty model.ManualModificationDraft.ValidationErrors then
            let modification = UIDirectoryModification.fromDto model.ManualModificationDraft.Modification
            { model with
                ManualModifications = { model.ManualModifications with Modifications = model.ManualModifications.Modifications @ [ modification ] }
                ManualModificationDraft = clearManualModificationDraft model.ManualModificationDraft
            }
        else
            model
    | ApplyModificationsResponse (Ok ())
    | ApplyModificationsResponse (Error _) -> { model with ModificationsState = Applied }

let init =
    {
        ModificationsState = Drafting
        AutoModifications = LoadingModifications
        ManualModifications = {
            IsEnabled = true
            Title = "Manual modifications"
            Kind = Update
            Modifications = []
        }
        ManualModificationDraft =
            let user =
                {
                    Name = UserName ""
                    SokratesId = None
                    FirstName = ""
                    LastName = ""
                    Type = Teacher
                }
            CreateUser (user, "") |> validateManualModificationDraft
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
            | UpdateGroup _ -> Fa.Solid.Sync, IsWarning
            | DeleteUser _
            | DeleteGroup _ -> Fa.Solid.Minus, IsDanger

        Panel.block [] [
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
        let memberText i =
            if i = 1 then sprintf "%d member" i
            else sprintf "%d members" i

        Panel.panel [] [
            directoryModificationGroupHeading directoryModificationGroup
            yield! List.map (directoryModificationView directoryModificationGroup) directoryModificationGroup.Modifications
        ]

    let autoModifications =
        match model.AutoModifications with
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

    let userTypeSelection (user: User) typeChangedMsg =
        [
            Field.div [] [
                Control.div [] [
                    let radioButtonName = System.Guid.NewGuid().ToString()
                    yield!
                        [
                            "Teacher", Teacher
                            "Student", Student (GroupName "")
                        ]
                        |> List.map (fun (title, data) ->
                            let isSelected =
                                match user.Type, data with
                                | Teacher, Teacher -> true
                                | Teacher, _ -> false
                                | Student _, Student _ -> true
                                | Student _, _ -> false
                            Radio.radio [] [
                                Radio.input [
                                    Radio.Input.Name radioButtonName
                                    Radio.Input.Props [
                                        Checked isSelected
                                        OnChange (fun _ -> dispatch (typeChangedMsg data))
                                    ]
                                ]
                                str title
                            ]
                        )
                ]
            ]
            match user.Type with
            | Teacher -> ()
            | Student (GroupName className) ->
                Field.div [] [
                    Control.div [] [
                        Input.text [
                            Input.Placeholder "Class name"
                            Input.Value className
                            Input.OnChange (fun ev -> dispatch (typeChangedMsg (Student (GroupName ev.Value))))
                        ]
                    ]
                ]
        ]

    let manualModificationForm =
        Container.container [] [
            Field.div [] [
                Control.div [] [
                    yield!
                        [
                            "Create user", CreateUser ({ Name = UserName ""; SokratesId = None; FirstName = ""; LastName = ""; Type = Teacher }, "")
                            "Delete user", DeleteUser { Name = UserName ""; SokratesId = None; FirstName = ""; LastName = ""; Type = Teacher }
                        ]
                        |> List.map (fun (title, data) ->
                            let isSelected =
                                match model.ManualModificationDraft.Modification, data with
                                | CreateUser _, CreateUser _ -> true
                                | CreateUser _, _ -> false
                                | UpdateUser _, _ -> false
                                | DeleteUser _, DeleteUser _ -> true
                                | DeleteUser _, _ -> false
                                | CreateGroup _, _ -> false
                                | UpdateGroup _, _ -> false
                                | DeleteGroup _, _ -> false
                            Radio.radio [] [
                                Radio.input [
                                    Radio.Input.Name "manual-modification-type"
                                    Radio.Input.Props [
                                        Checked isSelected
                                        OnChange (fun _ -> dispatch (SetManualModification data))
                                    ]
                                ]
                                str title
                            ]
                        )
                ]
            ]
            match model.ManualModificationDraft.Modification with
            | CreateUser (user, password) ->
                yield! userTypeSelection user (fun userType -> SetManualModification (CreateUser ({ user with Type = userType }, password)))
                Field.div [] [
                    Control.div [] [
                        Input.text [
                            Input.Placeholder "User name"
                            Input.Value (let (UserName userName) = user.Name in userName)
                            Input.OnChange (fun ev -> dispatch (SetManualModification (CreateUser ({ user with Name = UserName ev.Value }, password))))
                        ]
                    ]
                ]
                Field.div [] [
                    Control.div [] [
                        Input.text [
                            Input.Placeholder "First name"
                            Input.Value user.FirstName
                            Input.OnChange (fun ev -> dispatch (SetManualModification (CreateUser ({ user with FirstName = ev.Value }, password))))
                        ]
                    ]
                ]
                Field.div [] [
                    Control.div [] [
                        Input.text [
                            Input.Placeholder "Last name"
                            Input.Value user.LastName
                            Input.OnChange (fun ev -> dispatch (SetManualModification (CreateUser ({ user with LastName = ev.Value }, password))))
                        ]
                    ]
                ]
                Field.div [] [
                    Control.div [] [
                        Input.text [
                            Input.Placeholder "Password"
                            Input.Value password
                            Input.OnChange (fun ev -> dispatch (SetManualModification (CreateUser (user, ev.Value))))
                        ]
                    ]
                    Help.help [ Help.Color IsInfo ] [
                        str "For automatic modifications this defaults to the user's birthday in the format "
                        b [] [ str "dd.mm.yyyy" ]
                        str (sprintf ", e.g. 17.03.%d" (System.DateTime.Today.Year - 30))
                    ]
                ]
            | DeleteUser user ->
                yield! userTypeSelection user (fun userType -> SetManualModification (DeleteUser { user with Type = userType }))
                Field.div [] [
                    Control.div [] [
                        Input.text [
                            Input.Placeholder "User name"
                            Input.Value (let (UserName userName) = user.Name in userName)
                            Input.OnChange (fun ev -> dispatch (SetManualModification (DeleteUser { user with Name = UserName ev.Value })))
                        ]
                    ]
                ]
            | UpdateUser _
            | CreateGroup _
            | UpdateGroup _
            | DeleteGroup _ -> div [] [ str "Not implemented" ]

            Button.button
                [
                    Button.Disabled (isLocked || model.ManualModificationDraft.ValidationErrors.Length > 0)
                    Button.Color IsSuccess
                    Button.Props [
                        Title (model.ManualModificationDraft.ValidationErrors |> String.concat "\n")
                    ]
                    Button.OnClick (fun _ -> dispatch AddManualModification)
                ]
                [
                    Icon.icon [] [ Fa.i [ Fa.Solid.Plus ] [] ]
                    span [] [ str "Add modification" ]
                ]
        ]

    let manualModifications =
        Section.section [] [
            Panel.panel [] [
                directoryModificationGroupHeading model.ManualModifications
                yield! List.map (directoryModificationView model.ManualModifications) model.ManualModifications.Modifications
                Panel.block [] [
                    manualModificationForm
                ]
            ]
        ]

    Container.container [] [
        autoModifications
        manualModifications
        Button.list [] [
            Button.button
                [
                    Button.Disabled (ModificationsState.isApplying model.ModificationsState || LoadableDirectoryModifications.isLoading model.AutoModifications)
                    Button.OnClick (fun e -> dispatch LoadModifications)
                ]
                [
                    Icon.icon [] [ Fa.i [ Fa.Solid.Sync ] [] ]
                    span [] [ str "Reload auto modifications" ]
                ]
            Button.button
                [
                    Button.Disabled (ModificationsState.isApplied model.ModificationsState || LoadableDirectoryModifications.isLoading model.AutoModifications)
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

                msgs
                |> AsyncRx.startWith [ LoadModifications ]
                |> AsyncRx.choose (function | LoadModifications -> Some loadUpdates | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showSimpleErrorToast (fun e -> "Loading AD modifications failed", e.Message)
                |> AsyncRx.map LoadModificationsResponse

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
