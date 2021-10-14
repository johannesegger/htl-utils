module ModifyAD

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
                sprintf "%s %s" (user.LastName.ToUpper()) user.FirstName
            | UpdateUser ({ Type = Teacher } as user, ChangeUserName (UserName newUserName, newFirstName, newLastName, _)) ->
                let (UserName oldUserName) = user.Name
                sprintf "%s %s (%s) -> %s %s (%s)" (user.LastName.ToUpper()) user.FirstName oldUserName (newLastName.ToUpper()) newFirstName newUserName
            | UpdateUser ({ Type = Student (GroupName className) } as user, ChangeUserName (UserName newUserName, newFirstName, newLastName, _)) ->
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
            | CreateGroup Teacher ->
                "Teachers"
            | CreateGroup (Student (GroupName className)) ->
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

type ModificationsState =
    | Drafting
    | Applying of DirectoryModification list
    | Applied
module ModificationsState =
    let isDrafting = function | Drafting -> true | _ -> false
    let isApplying = function | Applying _ -> true | _ -> false
    let isApplied = function | Applied -> true | _ -> false

type ModificationDraft = {
    Modification: DirectoryModification
    ValidationErrors: string list
}

type Model = {
    ModificationsState: ModificationsState
    Modifications: UIDirectoryModification list
    ModificationDraft: ModificationDraft
}

type Msg =
    | ToggleEnableModification of UIDirectoryModification
    | SetModificationDraft of DirectoryModification
    | AddModification
    | ApplyModifications
    | ApplyModificationsResponse of Result<unit, exn>

let private validateModificationDraft directoryModification =
    {
        Modification = directoryModification
        ValidationErrors =
            [
                match directoryModification with
                | CreateUser ({ Name = UserName userName; Type = Teacher }, _, _) ->
                    if String.IsNullOrWhiteSpace userName then "User name must not be empty."
                | CreateUser ({ Name = UserName userName; Type = Student (GroupName className) }, _, _) ->
                    if String.IsNullOrWhiteSpace className then "Class name must not be empty."
                    if String.IsNullOrWhiteSpace userName then "User name must not be empty."
                | DeleteUser { Name = UserName userName } ->
                    if String.IsNullOrWhiteSpace userName then "User name must not be empty."
                | UpdateUser _
                | CreateGroup _
                | UpdateGroup _
                | DeleteGroup _ -> "Modification type not supported"
            ]
    }

let private clearModificationDraft modificationDraft =
    let clearedModification =
        match modificationDraft.Modification with
        | CreateUser (user, _, _) ->
            CreateUser ({ user with Name = UserName ""; FirstName = ""; LastName = "" }, [], "")
        | DeleteUser user ->
            DeleteUser { user with Name = UserName "" }
        | UpdateUser _
        | CreateGroup _
        | UpdateGroup _
        | DeleteGroup _ -> failwith "Not implemented"
    validateModificationDraft clearedModification

let rec update msg model =
    let updateDirectoryModification directoryModification fn =
        { model with
            Modifications =
                model.Modifications
                |> List.map (fun m -> if m = directoryModification then fn m else m)
        }

    match msg with
    | ToggleEnableModification (directoryModification) ->
        updateDirectoryModification directoryModification (fun p -> { p with IsEnabled = not p.IsEnabled })
    | ApplyModifications ->
        match model.ModificationsState with
        | Drafting -> { model with ModificationsState = Applying (List.choose UIDirectoryModification.toDto model.Modifications) }
        | Drafting
        | Applying _
        | Applied -> model
    | SetModificationDraft directoryModification ->
        { model with ModificationDraft = validateModificationDraft directoryModification }
    | AddModification ->
        if List.isEmpty model.ModificationDraft.ValidationErrors then
            let modification = UIDirectoryModification.fromDto model.ModificationDraft.Modification
            { model with
                Modifications = model.Modifications @ [ modification ]
                ModificationDraft = clearModificationDraft model.ModificationDraft
            }
        else
            model
    | ApplyModificationsResponse (Ok ())
    | ApplyModificationsResponse (Error _) -> { model with ModificationsState = Applied }

let init =
    {
        ModificationsState = Drafting
        Modifications = []
        ModificationDraft =
            let user =
                {
                    Name = UserName ""
                    SokratesId = None
                    FirstName = ""
                    LastName = ""
                    Type = Teacher
                }
            CreateUser (user, [], "") |> validateModificationDraft
    }

let view model dispatch =
    let isLocked = ModificationsState.isApplying model.ModificationsState || ModificationsState.isApplied model.ModificationsState

    let directoryModificationView (directoryModification: UIDirectoryModification) =
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
                    Button.Disabled (isLocked)
                    Button.Size IsSmall
                    Button.Color (if directoryModification.IsEnabled then color else NoColor)
                    Button.Props [ Style [ MarginRight "0.5rem" ] ]
                    Button.OnClick (fun _ -> dispatch (ToggleEnableModification directoryModification))
                ]
                [ Fa.i [ icon ] [] ]
            str directoryModification.Description
        ]

    let modificationsView =
        Section.section [] [
            Panel.panel [] [
                Panel.heading [] [
                    span [] [ str "Modifications" ]
                ]
                yield! List.map directoryModificationView model.Modifications
            ]
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

    let modificationForm =
        Container.container [] [
            Field.div [] [
                Control.div [] [
                    yield!
                        [
                            "Create user", CreateUser ({ Name = UserName ""; SokratesId = None; FirstName = ""; LastName = ""; Type = Teacher }, [], "")
                            "Delete user", DeleteUser { Name = UserName ""; SokratesId = None; FirstName = ""; LastName = ""; Type = Teacher }
                        ]
                        |> List.map (fun (title, data) ->
                            let isSelected =
                                match model.ModificationDraft.Modification, data with
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
                                    Radio.Input.Name "modification-type"
                                    Radio.Input.Props [
                                        Checked isSelected
                                        OnChange (fun _ -> dispatch (SetModificationDraft data))
                                    ]
                                ]
                                str title
                            ]
                        )
                ]
            ]
            match model.ModificationDraft.Modification with
            | CreateUser (user, mailAliases, password) ->
                yield! userTypeSelection user (fun userType -> SetModificationDraft (CreateUser ({ user with Type = userType }, mailAliases, password)))
                Field.div [] [
                    Control.div [] [
                        Input.text [
                            Input.Placeholder "User name"
                            Input.Value (let (UserName userName) = user.Name in userName)
                            Input.OnChange (fun ev -> dispatch (SetModificationDraft (CreateUser ({ user with Name = UserName ev.Value }, mailAliases, password))))
                        ]
                    ]
                ]
                Field.div [] [
                    Control.div [] [
                        Input.text [
                            Input.Placeholder "First name"
                            Input.Value user.FirstName
                            Input.OnChange (fun ev -> dispatch (SetModificationDraft (CreateUser ({ user with FirstName = ev.Value }, mailAliases, password))))
                        ]
                    ]
                ]
                Field.div [] [
                    Control.div [] [
                        Input.text [
                            Input.Placeholder "Last name"
                            Input.Value user.LastName
                            Input.OnChange (fun ev -> dispatch (SetModificationDraft (CreateUser ({ user with LastName = ev.Value }, mailAliases, password))))
                        ]
                    ]
                ]
                Field.div [] [
                    Control.div [] [
                        Input.text [
                            Input.Placeholder "Password"
                            Input.Value password
                            Input.OnChange (fun ev -> dispatch (SetModificationDraft (CreateUser (user, mailAliases, ev.Value))))
                        ]
                    ]
                    Help.help [ Help.Color IsInfo ] [
                        str "For automatic modifications this defaults to the user's birthday in the format "
                        b [] [ str "dd.mm.yyyy" ]
                        str (sprintf ", e.g. 17.03.%d" (System.DateTime.Today.Year - 30))
                    ]
                ]
            | DeleteUser user ->
                yield! userTypeSelection user (fun userType -> SetModificationDraft (DeleteUser { user with Type = userType }))
                Field.div [] [
                    Control.div [] [
                        Input.text [
                            Input.Placeholder "User name"
                            Input.Value (let (UserName userName) = user.Name in userName)
                            Input.OnChange (fun ev -> dispatch (SetModificationDraft (DeleteUser { user with Name = UserName ev.Value })))
                        ]
                    ]
                ]
            | UpdateUser _
            | CreateGroup _
            | UpdateGroup _
            | DeleteGroup _ -> div [] [ str "Not implemented" ]

            Button.button
                [
                    Button.Disabled (isLocked || model.ModificationDraft.ValidationErrors.Length > 0)
                    Button.Color IsSuccess
                    Button.Props [
                        Title (model.ModificationDraft.ValidationErrors |> String.concat "\n")
                    ]
                    Button.OnClick (fun _ -> dispatch AddModification)
                ]
                [
                    Icon.icon [] [ Fa.i [ Fa.Solid.Plus ] [] ]
                    span [] [ str "Add modification" ]
                ]
        ]

    Container.container [] [
        if not <| List.isEmpty model.Modifications then modificationsView
        Section.section [] [ modificationForm ]
        Section.section [] [
            Button.list [] [
                Button.button
                    [
                        Button.Disabled (ModificationsState.isApplied model.ModificationsState || List.isEmpty model.Modifications)
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
    ]

let stream getAuthRequestHeader (pageActive: IAsyncObservable<bool>) (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
    pageActive
    |> AsyncRx.flatMapLatest (function
        | true ->
            [
                msgs

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
