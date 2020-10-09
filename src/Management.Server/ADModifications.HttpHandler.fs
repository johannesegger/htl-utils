module ADModifications.HttpHandler

open ADModifications.DataTransferTypes
open ADModifications.Mapping
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open System
open System.Globalization

let userNameFromName (firstName: string) (lastName: string) =
    [String.cut 1 firstName; lastName]
    |> List.map String.asAlphaNumeric
    |> String.concat "."
    |> UserName

let uniqueUserName (UserName rawUserName) existingUserNames =
    Seq.initInfinite ((+)2)
    |> Seq.map string
    |> Seq.append [ "" ]
    |> Seq.map (fun number -> sprintf "%s%s" (String.cut (20 - number.Length) rawUserName) number |> UserName)
    |> Seq.find (fun name -> not <| List.contains name existingUserNames)

let modifications (sokratesTeachers: Sokrates.Domain.Teacher list) (sokratesStudents: Sokrates.Domain.Student list) (adUsers: AD.Domain.ExistingUser list) =
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
        [
            yield!
                sokratesStudents
                |> List.map (fun student -> Student (GroupName student.SchoolClass))
            if not <| List.isEmpty sokratesTeachers then Teacher
        ]
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
                    if adUser.SokratesId |> Option.map SokratesId.fromADDto <> Some (SokratesId.fromSokratesDto teacher.Id) then
                        UpdateUser (User.fromADDto adUser, SetSokratesId (SokratesId.fromSokratesDto teacher.Id))
                    if adUser.FirstName <> teacher.FirstName || adUser.LastName <> teacher.LastName then
                        UpdateUser (User.fromADDto adUser, ChangeUserName (UserName teacher.ShortName, teacher.FirstName, teacher.LastName))
                ]
        )
    let createOrUpdateStudentModifications =
        let adUserNames = adUsers |> List.map (fun adUser -> UserName.fromADDto adUser.Name)
        (([], []), sokratesStudents)
        ||> List.fold (fun (newUserNames, modifications) student ->
            let studentId = SokratesId.fromSokratesDto student.Id
            match Map.tryFind studentId adUserLookupBySokratesId with
            | None ->
                let existingUserNames = adUserNames @ newUserNames
                let rawUserName = userNameFromName student.FirstName1 student.LastName
                let userName = uniqueUserName rawUserName existingUserNames
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
                                uniqueUserName rawUserName existingUserNames
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
        |> List.rev
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
        |> Seq.map (fun userType -> (CreateGroup userType))
        |> Seq.toList
    let deleteGroupModifications =
        Set.difference adUserTypes sokratesUserTypes
        |> Seq.map (fun userType -> (DeleteGroup userType))
        |> Seq.toList

    [
        createGroupModifications
        createOrUpdateTeacherModifications
        createOrUpdateStudentModifications
        deleteUserModifications
        deleteGroupModifications
    ]
    |> List.concat

let getADModifications adConfig sokratesConfig : HttpHandler =
    fun next ctx -> task {
        let! sokratesTeachers = Sokrates.Core.getTeachers |> Reader.run sokratesConfig |> Async.StartChild
        let timestamp =
            ctx.TryGetQueryStringValue "date"
            |> Option.map (fun date ->
                tryDo (fun () -> (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.CurrentCulture, DateTimeStyles.None))) ()
                |> Option.defaultWith (fun () -> failwithf "Can't parse \"%s\"" date)
            )
        let! sokratesStudents = Sokrates.Core.getStudents None timestamp |> Reader.run sokratesConfig |> Async.StartChild
        let adUsers = Reader.run adConfig AD.Core.getUsers

        let! sokratesTeachers = sokratesTeachers
        let! sokratesStudents = sokratesStudents

        let modifications = modifications sokratesTeachers sokratesStudents adUsers
        return! Successful.OK modifications next ctx
    }

let applyADModifications adConfig : HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<DirectoryModification list>()
        data
        |> List.map DirectoryModification.toADDto
        |> AD.Core.applyDirectoryModifications
        |> Reader.run adConfig
        return! Successful.OK () next ctx
    }

let getADIncrementClassGroupUpdates adConfig incrementClassGroupsConfig : HttpHandler =
    fun next ctx -> task {
        let classGroups =
            Reader.run adConfig AD.Core.getClassGroups
            |> List.map (GroupName.fromADDto >> (fun (GroupName groupName) -> groupName))

        let modifications = IncrementClassGroups.Core.modifications classGroups |> Reader.run incrementClassGroupsConfig
        return! Successful.OK modifications next ctx
    }

let applyADIncrementClassGroupUpdates adConfig : HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<IncrementClassGroups.DataTransferTypes.ClassGroupModification list>()
        data
        |> List.map (ClassGroupModification.toDirectoryModification >> DirectoryModification.toADDto)
        |> AD.Core.applyDirectoryModifications
        |> Reader.run adConfig
        return! Successful.OK () next ctx
    }
