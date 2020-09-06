module ADModifications.HttpHandler

open ADModifications.DataTransferTypes
open ADModifications.Mapping
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open System
open System.Text.RegularExpressions

let private userNameFromName (firstName: string) (lastName: string) =
    [firstName; lastName]
    |> List.map (
        String.replace "Ä" "Ae"
        >> String.replace "Ö" "Oe"
        >> String.replace "Ü" "Ue"
        >> String.replace "ä" "ae"
        >> String.replace "ö" "oe"
        >> String.replace "ü" "ue"
        >> Unidecode.NET.Unidecoder.Unidecode
    )
    |> String.concat "."

let private uniqueUserName (UserName rawUserName) existingUserNames =
    Seq.initInfinite ((+)2)
    |> Seq.map string
    |> Seq.append [ "" ]
    |> Seq.map (sprintf "%s%s" rawUserName >> UserName)
    |> Seq.find (fun name -> not <| List.contains name existingUserNames)

let private modifications (sokratesTeachers: Sokrates.Domain.Teacher list) (sokratesStudents: Sokrates.Domain.Student list) (adUsers: AD.Domain.User list) =
    let adUserLookupBySokratesId =
        adUsers
        |> List.choose (fun user -> user.SokratesId |> Option.map (fun sokratesId -> SokratesId.fromADDto sokratesId, user))
        |> Map.ofList
    let adUserLookupByUserName =
        adUsers
        |> List.map (fun user -> UserName.fromADDto user.Name, user)
        |> Map.ofList
    let sokratesIds =
        [
            yield! sokratesTeachers |> List.map (fun teacher -> teacher.Id)
            yield! sokratesStudents |> List.map (fun student -> student.Id)
        ]
        |> List.map SokratesId.fromSokratesDto
        |> Set.ofList
    let sokratesTeacherNames = sokratesTeachers |> List.map (fun teacher -> teacher.ShortName) |> Set.ofList
    let adUserTypes =
        adUsers
        |> List.map (fun adUser -> UserType.fromADDto adUser.Type)
        |> Set.ofList
    let sokratesUserTypes =
        sokratesStudents
        |> List.map (fun student -> Student (GroupName student.SchoolClass))
        |> List.append [ Teacher ]
        |> Set.ofList

    let tryFindADTeacher sokratesId userName =
        Map.tryFind sokratesId adUserLookupBySokratesId
        |> Option.orElseWith (fun () -> Map.tryFind userName adUserLookupByUserName)

    let createOrUpdateTeacherModifications =
        sokratesTeachers
        |> List.collect (fun teacher ->
            let sokratesId = SokratesId.fromSokratesDto teacher.Id
            let userName = UserName teacher.ShortName
            match tryFindADTeacher sokratesId userName with
            | None ->
                let user = User.fromSokratesTeacherDto teacher
                [ CreateUser (user, teacher.DateOfBirth.ToString("dd.MM.yyyy")) ]
            | Some adUser ->
                [
                    if adUser.FirstName <> teacher.FirstName || adUser.LastName <> teacher.LastName then
                        UpdateUser (User.fromADDto adUser, ChangeUserName (UserName teacher.ShortName, teacher.FirstName, teacher.LastName))
                    elif adUser.SokratesId |> Option.map SokratesId.fromADDto <> Some (SokratesId.fromSokratesDto teacher.Id) then
                        UpdateUser (User.fromADDto adUser, SetSokratesId (SokratesId.fromSokratesDto teacher.Id))
                ]
        )
    let createOrUpdateStudentModifications =
        let adUserNames = adUsers |> List.map (fun adUser -> UserName.fromADDto adUser.Name)
        (sokratesStudents, ([], []))
        ||> List.foldBack (fun student (newUserNames, modifications) ->
            let studentId = SokratesId.fromSokratesDto student.Id
            match Map.tryFind studentId adUserLookupBySokratesId with
            | None ->
                let existingUserNames = adUserNames @ newUserNames
                let rawUserName = userNameFromName student.FirstName1 student.LastName
                let userName = uniqueUserName (UserName rawUserName) existingUserNames
                let user = User.fromSokratesStudentDto student userName
                let modification = CreateUser (user, student.DateOfBirth.ToString("dd.MM.yyyy"))
                (userName :: newUserNames, modification :: modifications)
            | Some adUser ->
                let (newUserNames, modifications) =
                    if adUser.FirstName <> student.FirstName1 || adUser.LastName <> student.LastName then
                        let rawUserName = userNameFromName student.FirstName1 student.LastName
                        let userName =
                            let oldRawUserName = userNameFromName adUser.FirstName adUser.LastName
                            if oldRawUserName = rawUserName then UserName.fromADDto adUser.Name
                            else
                                let existingUserNames = (adUserNames |> List.except [ UserName.fromADDto adUser.Name ]) @ newUserNames
                                uniqueUserName (UserName rawUserName) existingUserNames
                        let user = User.fromADDto adUser
                        let modification = UpdateUser (user, ChangeUserName (userName, student.FirstName1, student.LastName))
                        (userName :: newUserNames, modification :: modifications)
                    else
                        (newUserNames, modifications)

                // Not necessary today, but might become useful if lookup strategy changes
                let (newUserNames, modifications) =
                    if adUser.SokratesId |> Option.map SokratesId.fromADDto <> Some (SokratesId.fromSokratesDto student.Id) then
                        let user = User.fromADDto adUser
                        let modification = UpdateUser (user, SetSokratesId (SokratesId.fromSokratesDto student.Id))
                        (newUserNames, modification :: modifications)
                    else
                        (newUserNames, modifications)

                let (newUserNames, modifications) =
                    if UserType.fromADDto adUser.Type <> Student (GroupName student.SchoolClass) then
                        let user = User.fromADDto adUser
                        let modification = UpdateUser (user, MoveStudentToClass (GroupName student.SchoolClass))
                        (newUserNames, modification :: modifications)
                    else
                        (newUserNames, modifications)
                (newUserNames, modifications)
        )
        |> snd
    let deleteUserModifications =
        adUsers
        |> List.choose (fun adUser ->
            let isInSokrates =
                match adUser.Type with
                | AD.Domain.Teacher ->
                    adUser.SokratesId
                    |> Option.map (fun sokratesId -> Set.contains (SokratesId.fromADDto sokratesId) sokratesIds)
                    |> Option.defaultWith (fun () -> let (AD.Domain.UserName userName) = adUser.Name in Set.contains userName sokratesTeacherNames)
                | AD.Domain.Student _ ->
                    adUser.SokratesId
                    |> Option.map (fun sokratesId -> Set.contains (SokratesId.fromADDto sokratesId) sokratesIds)
                    |> Option.defaultValue false
            if isInSokrates then None
            else
                let user = User.fromADDto adUser
                Some (DeleteUser user)
        )
    let createGroupModifications =
        Set.difference sokratesUserTypes adUserTypes
        |> Seq.map (fun userType -> (CreateGroup (userType, [])))
        |> Seq.toList
    let deleteGroupModifications =
        Set.difference adUserTypes sokratesUserTypes
        |> Seq.map (fun userType -> (DeleteGroup userType))
        |> Seq.toList

    [
        createOrUpdateTeacherModifications
        createOrUpdateStudentModifications
        deleteUserModifications
        createGroupModifications
        deleteGroupModifications
    ]
    |> List.concat

let getADModifications : HttpHandler =
    fun next ctx -> task {
        let! sokratesTeachers = Sokrates.Core.getTeachers () |> Async.StartChild
        let! sokratesStudents = Sokrates.Core.getStudents None None |> Async.StartChild
        let adUsers = AD.Core.getUsers ()

        let! sokratesTeachers = sokratesTeachers
        let! sokratesStudents = sokratesStudents

        let modifications = modifications sokratesTeachers sokratesStudents adUsers
        return! Successful.OK modifications next ctx
    }

let applyADModifications : HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<DirectoryModification list>()
        data
        |> List.map DirectoryModification.toADDto
        |> AD.Core.applyDirectoryModifications
        return! Successful.OK () next ctx
    }

let getADIncrementClassGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let adClassGroups = AD.Core.getClassGroups ()

        let classLevels =
            Environment.getEnvVarOrFail "MGMT_CLASS_LEVELS"
            |> String.split ";"
            |> Seq.mapi (fun index row ->
                let parts = row.Split ","
                let title =
                    Array.tryItem 0 parts
                    |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of class levels setting: Can't get title" row)
                let pattern =
                    Array.tryItem 1 parts
                    |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of class levels setting: Can't get pattern" row)
                let maxLevel =
                    Array.tryItem 2 parts
                    |> Option.bind (tryDo Int32.TryParse)
                    |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of class levels setting: Can't parse max level" row)
                (index, title, pattern, maxLevel)
            )
            |> Seq.toList

        let modifications =
            adClassGroups
            |> Seq.choose (GroupName.fromADDto >> fun (GroupName groupName) ->
                classLevels
                |> List.choose (fun (index, title, pattern, maxLevel) ->
                    let m =
                        try
                            Regex.Match(groupName, pattern)
                        with e -> failwithf "Error while matching group name \"%s\" with pattern \"%s\": %s" groupName pattern e.Message
                    if m.Success then
                        let classLevel =
                            m.Value
                            |> tryDo Int32.TryParse
                            |> Option.defaultWith (fun () -> failwithf "Pattern \"%s\" doesn't match class level of \"%s\" as number" pattern groupName)
                        if classLevel < maxLevel then
                            let newName = Regex.Replace(groupName, pattern, string (classLevel + 1))
                            Some ((index, title), classLevel, ChangeClassGroupName (GroupName groupName, GroupName newName))
                        else
                            Some ((index, title), classLevel, DeleteClassGroup (GroupName groupName))
                    else None
                )
                |> function
                | [] -> None
                | [ x ] -> Some x
                | _ -> failwithf "Class \"%s\" was matched by multiple patterns" groupName
            )
            |> Seq.groupBy(fun (group, _, _) -> group)
            |> Seq.sortBy fst
            |> Seq.map (fun ((_, title), modifications) ->
                {
                    Title = title
                    Modifications =
                        modifications
                        |> Seq.sortByDescending (fun (_, classLevel, _) -> classLevel)
                        |> Seq.map (fun (_, _, modification) -> modification)
                        |> Seq.toList
                }
            )
            |> Seq.toList
        return! Successful.OK modifications next ctx
    }

let applyADIncrementClassGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<ClassGroupModification list>()
        data
        |> List.map (ClassGroupModification.toDirectoryModification >> DirectoryModification.toADDto)
        |> AD.Core.applyDirectoryModifications
        return! Successful.OK () next ctx
    }
