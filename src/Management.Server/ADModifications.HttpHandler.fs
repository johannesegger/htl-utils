module ADModifications.HttpHandler

open ADModifications.DataTransferTypes
open ADModifications.Mapping
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open System
open System.Globalization
open System.Text.RegularExpressions

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

let rawMailAliases user =
    [
        {
            IsPrimary = true
            UserName = sprintf "%s.%s" user.FirstName user.LastName
            Domain = DefaultDomain
        }
    ]

let uniqueMailAliases user existingMailAliasNames =
    rawMailAliases user
    |> List.map (fun rawMailAliasName ->
        Seq.initInfinite (fun i -> i + 2)
        |> Seq.map string
        |> Seq.append [ "" ]
        |> Seq.map (fun number -> { rawMailAliasName with UserName = sprintf "%s%s" rawMailAliasName.UserName number } )
        |> Seq.find (fun name -> not <| List.contains name.UserName existingMailAliasNames)
    )

let modifications (sokratesTeachers: Sokrates.Domain.Teacher list) (sokratesStudents: Sokrates.Domain.Student list) (adUsers: AD.Domain.ExistingUser list) =
    let sokratesIds =
        [
            yield! sokratesTeachers |> List.map (fun teacher -> teacher.Id)
            yield! sokratesStudents |> List.map (fun student -> student.Id)
        ]
        |> List.map SokratesId.fromSokratesDto
        |> Set.ofList
    let sokratesTeacherNames =
        sokratesTeachers
        |> List.map (fun teacher -> teacher.ShortName)
        |> Set.ofList
    let (adUsersToKeep, adUsersToDelete) =
        adUsers
        |> List.partition (fun adUser ->
            match adUser.Type with
            | AD.Domain.Teacher ->
                adUser.SokratesId
                |> Option.map (fun sokratesId -> Set.contains (SokratesId.fromADDto sokratesId) sokratesIds)
                |> Option.defaultWith (fun () -> let (AD.Domain.UserName userName) = adUser.Name in Set.contains userName sokratesTeacherNames)
            | AD.Domain.Student _ ->
                adUser.SokratesId
                |> Option.map (fun sokratesId -> Set.contains (SokratesId.fromADDto sokratesId) sokratesIds)
                |> Option.defaultValue false
        )
    let adUserLookupBySokratesId =
        adUsers
        |> List.choose (fun user -> user.SokratesId |> Option.map (fun sokratesId -> SokratesId.fromADDto sokratesId, user))
        |> Map.ofList
    let adUserLookupByUserName =
        adUsers
        |> List.map (fun user -> UserName.fromADDto user.Name, user)
        |> Map.ofList
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

    let adUserNames =
        adUsersToKeep
        |> List.map (fun adUser -> UserName.fromADDto adUser.Name)

    let adUserMailAliasNames =
        adUsersToKeep
        |> List.collect (fun adUser -> adUser.ProxyAddresses)
        |> List.map (MailAlias.fromADProxyAddress >> fun v -> v.UserName)

    let (existingMailAliasNames, createOrUpdateTeacherModifications) =
        ((adUserMailAliasNames, []), sokratesTeachers)
        ||> List.fold (fun (existingMailAliasNames, modifications) teacher ->
            let sokratesId = SokratesId.fromSokratesDto teacher.Id
            let userName = UserName teacher.ShortName
            match tryFindADTeacher sokratesId userName with
            | None ->
                let user = User.fromSokratesTeacherDto teacher
                let mailAliasNames = uniqueMailAliases user existingMailAliasNames
                let modification = CreateUser (user, mailAliasNames, teacher.DateOfBirth.ToString("dd.MM.yyyy"))
                (mailAliasNames |> List.map (fun v -> v.UserName)) @ existingMailAliasNames, modification :: modifications
            | Some adUser ->
                let changes =
                    [
                        if adUser.SokratesId |> Option.map SokratesId.fromADDto <> Some (SokratesId.fromSokratesDto teacher.Id) then
                            [], [ UpdateUser (User.fromADDto adUser, SetSokratesId (SokratesId.fromSokratesDto teacher.Id)) ]
                        if adUser.FirstName <> teacher.FirstName || adUser.LastName <> teacher.LastName then
                            let user = User.fromSokratesTeacherDto teacher
                            let otherMailAliasNames = existingMailAliasNames |> List.except (adUser.ProxyAddresses |> List.map (fun v -> v.Address.UserName))
                            let mailAliasNames = uniqueMailAliases user otherMailAliasNames
                            let previousMailAliasNames =
                                adUser.ProxyAddresses
                                |> List.map (MailAlias.fromADProxyAddress >> MailAlias.toNonPrimary)
                            (mailAliasNames |> List.map (fun v -> v.UserName)) @ otherMailAliasNames, [ UpdateUser (User.fromADDto adUser, ChangeUserName (UserName teacher.ShortName, teacher.FirstName, teacher.LastName, mailAliasNames @ previousMailAliasNames)) ]
                    ]
                changes |> List.collect fst, changes |> List.collect snd
        )

    let createOrUpdateStudentModifications =
        (((adUserNames, existingMailAliasNames), []), sokratesStudents)
        ||> List.fold (fun ((existingUserNames, existingMailAliasNames), modifications) student ->
            let studentId = SokratesId.fromSokratesDto student.Id
            match Map.tryFind studentId adUserLookupBySokratesId with
            | None ->
                let rawUserName = userNameFromName student.FirstName1 student.LastName
                let userName = uniqueUserName rawUserName existingUserNames
                let user = User.fromSokratesStudentDto student userName
                let mailAliasNames = uniqueMailAliases user existingMailAliasNames
                let modification = CreateUser (user, mailAliasNames, student.DateOfBirth.ToString("dd.MM.yyyy"))
                ((userName :: existingUserNames, (mailAliasNames |> List.map (fun v -> v.UserName)) @ existingMailAliasNames), modification :: modifications)
            | Some adUser ->
                let (existingUserNames, existingMailAliases, modifications) =
                    if adUser.FirstName <> student.FirstName1 || adUser.LastName <> student.LastName then
                        let rawUserName = userNameFromName student.FirstName1 student.LastName
                        let otherUserNames = existingUserNames |> List.except [ UserName.fromADDto adUser.Name ]
                        let userName =
                            let oldRawUserName = userNameFromName adUser.FirstName adUser.LastName
                            if oldRawUserName = rawUserName then UserName.fromADDto adUser.Name
                            else uniqueUserName rawUserName otherUserNames
                        let user = User.fromADDto adUser
                        let otherMailAliasNames = existingMailAliasNames |> List.except (adUser.ProxyAddresses |> List.map (fun v -> v.Address.UserName))
                        let mailAliasNames = uniqueMailAliases user otherMailAliasNames
                        let modification = UpdateUser (user, ChangeUserName (userName, student.FirstName1, student.LastName, mailAliasNames))
                        (userName :: otherUserNames, (mailAliasNames |> List.map (fun v -> v.UserName)) @ otherMailAliasNames, modification :: modifications)
                    else
                        (existingUserNames, existingMailAliasNames, modifications)

                // Not necessary today, but might become useful if lookup strategy changes
                let modifications =
                    if adUser.SokratesId |> Option.map SokratesId.fromADDto <> Some (SokratesId.fromSokratesDto student.Id) then
                        let user = User.fromADDto adUser
                        let modification = UpdateUser (user, SetSokratesId (SokratesId.fromSokratesDto student.Id))
                        modification :: modifications
                    else
                        modifications

                let modifications =
                    if UserType.fromADDto adUser.Type <> Student (GroupName student.SchoolClass) then
                        let user = User.fromADDto adUser
                        let modification = UpdateUser (user, MoveStudentToClass (GroupName student.SchoolClass))
                        modification :: modifications
                    else
                        modifications
                ((existingUserNames, existingMailAliases), modifications)
        )
        |> snd
        |> List.rev
    let deleteUserModifications =
        adUsersToDelete
        |> List.map (User.fromADDto >> DeleteUser)
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
        deleteUserModifications // Free user names before creating/changing existing ones
        createOrUpdateTeacherModifications
        createOrUpdateStudentModifications
        deleteGroupModifications // All users have to be moved/deleted before we can delete the group
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
