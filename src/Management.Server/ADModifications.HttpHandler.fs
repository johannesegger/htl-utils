module ADModifications.HttpHandler

open ADModifications.DataTransferTypes
open ADModifications.Mapping
open Giraffe
open System
open System.Globalization

type ExistingUser = {
    User: User
    MailAddressNames: string list
}
module ExistingUser =
    let fromADDto (adUser: AD.ExistingUser) =
        {
            User = User.fromADDto adUser
            MailAddressNames = [
                yield adUser.UserPrincipalName.UserName
                for v in adUser.ProxyAddresses -> v.Address.UserName
            ]
        }

let userNameFromName (firstName: string) (lastName: string) =
    [String.cut 1 firstName; lastName]
    |> List.map String.asAlphaNumeric
    |> String.concat "."
    |> UserName

let uniqueUserName existingUserNames (UserName rawUserName) =
    Seq.initInfinite ((+)2)
    |> Seq.map string
    |> Seq.append [ "" ]
    |> Seq.map (fun number -> sprintf "%s%s" (String.cut (20 - number.Length) rawUserName) number)
    |> Seq.find (fun name -> not <| List.exists (fun (UserName v) -> CIString name = CIString v) existingUserNames)
    |> UserName

let rawMailAliases user =
    [
        {
            IsPrimary = true
            UserName = sprintf "%s.%s" user.FirstName user.LastName
        }
    ]

let uniqueMailAliases user (existingUsers: ExistingUser list) =
    let existingMailAddressNames =
        existingUsers
        |> List.collect (fun v -> v.MailAddressNames)
    rawMailAliases user
    |> List.map (fun rawMailAliasName ->
        Seq.initInfinite (fun i -> i + 2)
        |> Seq.map string
        |> Seq.append [ "" ]
        |> Seq.map (fun number -> { rawMailAliasName with UserName = sprintf "%s%s" rawMailAliasName.UserName number } )
        |> Seq.find (fun name -> not <| List.contains name.UserName existingMailAddressNames)
    )

let tryFindUser (users: User list) sokratesId userName =
    sokratesId
    |> Option.bind (fun sokratesId ->
        users
        |> List.tryFind (fun v -> v.SokratesId = Some sokratesId)
    )
    |> Option.orElseWith (fun () ->
        userName
        |> Option.bind (fun userName ->
            users
            |> List.tryFind (fun v -> v.Name = userName)
        )
    )

let tryFindExistingUser (users: ExistingUser list) sokratesId userName =
    sokratesId
    |> Option.bind (fun sokratesId ->
        users
        |> List.tryFind (fun v -> v.User.SokratesId = Some sokratesId)
    )
    |> Option.orElseWith (fun () ->
        userName
        |> Option.bind (fun userName ->
            users
            |> List.tryFind (fun v -> v.User.Name = userName)
        )
    )

let getMailAddressNames (adUser: AD.ExistingUser) =
    [
        adUser.UserPrincipalName.UserName
        yield! adUser.ProxyAddresses |> List.map (fun v -> v.Address.UserName)
    ]

let calculateDeleteTeacherModification sokratesTeachers (existingUser: ExistingUser) existingUsers =
    match existingUser.User.Type with
    | Teacher ->
        let sokratesUsers =
            sokratesTeachers
            |> List.map User.fromSokratesTeacherDto
        match tryFindUser sokratesUsers existingUser.User.SokratesId (Some existingUser.User.Name) with
        | None ->
            let existingUsers = existingUsers |> List.except [ existingUser ]
            let modification = DeleteUser existingUser.User
            Some (modification, existingUsers)
        | Some _ -> None
    | _ -> None

let calculateDeleteStudentModification sokratesStudents (existingUser: ExistingUser) existingUsers =
    match existingUser.User.Type with
    | Student _ ->
        let sokratesUsers =
            sokratesStudents
            |> List.map (fun (v: Sokrates.Student) ->
                let rawUserName = userNameFromName v.FirstName1 v.LastName
                User.fromSokratesStudentDto v rawUserName
            )
        match tryFindUser sokratesUsers existingUser.User.SokratesId None with
        | None ->
            let existingUsers = existingUsers |> List.except [ existingUser ]
            let modification = DeleteUser existingUser.User
            Some (modification, existingUsers)
        | Some _ -> None
    | _ -> None

let calculateCreateTeacherModification users (teacher: User) password =
    match tryFindExistingUser users teacher.SokratesId (Some teacher.Name) with
    | Some _existingUser -> None
    | None ->
        let mailAliases = uniqueMailAliases teacher users
        let modification = CreateUser (teacher, mailAliases, password)
        let newUser = {
            User = teacher
            MailAddressNames = [
                let (UserName v) = teacher.Name in v // TODO assuming username becomes a mail address is not clean
                yield! mailAliases |> List.map (fun v -> v.UserName)
            ]
        }
        Some (modification, newUser :: users)

let calculateCreateStudentModification users (student: User) password =
    match tryFindExistingUser users student.SokratesId None with
    | Some _existingUser -> None
    | None ->
        let student =
            let existingUserNames =
                users
                |> List.map (fun v -> v.User.Name)
            let userName =
                userNameFromName student.FirstName student.LastName
                |> uniqueUserName existingUserNames
            { student with Name = userName }
        let mailAliases = uniqueMailAliases student users
        let modification = CreateUser (student, mailAliases, password)
        let newUser = {
            User = student
            MailAddressNames = [
                let (UserName v) = student.Name in v // TODO assuming username becomes a mail address is not clean
                yield! mailAliases |> List.map (fun v -> v.UserName)
            ]
        }
        Some (modification, newUser :: users)

let calculateTeacherSokratesIdUpdateModification users teacher =
    match tryFindExistingUser users teacher.SokratesId (Some teacher.Name), teacher.SokratesId with
    | Some existingUser, Some newSokratesId when existingUser.User.SokratesId <> Some newSokratesId ->
        let modification = UpdateUser (existingUser.User, SetSokratesId newSokratesId)
        let users =
            users
            |> List.map (fun v ->
                if v = existingUser then { v with User = { v.User with SokratesId = Some newSokratesId } }
                else v
            )
        Some (modification, users)
    | _ -> None

let calculateChangeTeacherNameModification (users: ExistingUser list) (teacher: User) =
    match tryFindExistingUser users teacher.SokratesId (Some teacher.Name) with
    | Some user when user.User.Name <> teacher.Name || user.User.FirstName <> teacher.FirstName || user.User.LastName <> teacher.LastName ->
        let mailAliases = [
            yield! uniqueMailAliases teacher (users |> List.except [ user ])
            yield! user.MailAddressNames |> List.map (fun v -> { IsPrimary = false; UserName = v })
        ]
        let modification = UpdateUser (user.User, ChangeUserName (teacher.Name, teacher.FirstName, teacher.LastName, mailAliases))
        let users =
            users
            |> List.map (fun v ->
                if v = user then
                    { v with
                        User = {
                            v.User with
                                Name = teacher.Name
                                FirstName = teacher.FirstName
                                LastName = teacher.LastName
                        }
                        MailAddressNames = [
                            let (UserName v) = teacher.Name in v // TODO assuming username becomes a mail address is not clean
                            yield! mailAliases |> List.map (fun v -> v.UserName)
                        ]
                    }
                else v
            )
        Some (modification, users)
    | _ -> None

let calculateChangeStudentNameModification (users: ExistingUser list) (student: User) =
    match tryFindExistingUser users student.SokratesId None with
    | Some user when user.User.FirstName <> student.FirstName || user.User.LastName <> student.LastName ->
        let student =
            let existingUserNames =
                users
                |> List.except [ user ]
                |> List.map (fun v -> v.User.Name)
            let newUserName =
                userNameFromName student.FirstName student.LastName
                |> uniqueUserName existingUserNames
            { student with Name = newUserName }
        let mailAliases = [
            yield! uniqueMailAliases student (users |> List.except [ user ])
            yield! user.MailAddressNames |> List.map (fun v -> { IsPrimary = false; UserName = v })
        ]
        let modification = UpdateUser (user.User, ChangeUserName (student.Name, student.FirstName, student.LastName, mailAliases))
        let users =
            users
            |> List.map (fun v ->
                if v = user then
                    { v with
                        User = {
                            v.User with
                                Name = student.Name
                                FirstName = student.FirstName
                                LastName = student.LastName
                        }
                        MailAddressNames = [
                            let (UserName v) = student.Name in v // TODO assuming username becomes a mail address is not clean
                            yield! mailAliases |> List.map (fun v -> v.UserName)
                        ]
                    }
                else v
            )
        Some (modification, users)
    | _ -> None

let calculateMoveStudentToClassModifications users student =
    match tryFindExistingUser users student.SokratesId None, student.Type with
    | Some user, Student group when user.User.Type <> student.Type ->
        let modification = UpdateUser (user.User, MoveStudentToClass group)
        let users =
            users
            |> List.map (fun v ->
                if v = user then
                    { v with
                        User = {
                            v.User with
                                Type = Student group
                        }
                    }
                else v
            )
        Some (modification, users)
    | _ -> None

let private getExistingUserTypes existingUsers =
    existingUsers
    |> List.map (fun existingUser -> existingUser.User.Type)
    |> Set.ofList

let private getSokratesUserTypes (sokratesTeachers: Sokrates.Teacher list) (sokratesStudents: Sokrates.Student list) =
    [
        yield!
            sokratesStudents
            |> List.map (fun student -> Student (ClassName student.SchoolClass))
        if not <| List.isEmpty sokratesTeachers then Teacher
    ]
    |> Set.ofList

let calculateCreateGroupModifications existingUsers sokratesTeachers sokratesStudents =
    Set.difference (getSokratesUserTypes sokratesTeachers sokratesStudents) (getExistingUserTypes existingUsers)
    |> Seq.map (fun userType -> (CreateGroup userType))
    |> Seq.toList

let calculateDeleteGroupModifications existingUsers sokratesTeachers sokratesStudents =
    Set.difference (getExistingUserTypes existingUsers) (getSokratesUserTypes sokratesTeachers sokratesStudents)
    |> Seq.map (fun userType -> (DeleteGroup userType))
    |> Seq.toList

let modifications sokratesTeachers sokratesStudents adUsers =
    let existingAdUsers =
        adUsers
        |> List.map ExistingUser.fromADDto
    let modifications = []
    let state = (existingAdUsers, modifications)

    let state =
        let (existingUsers, modifications) = state
        let modifications =
            (modifications, calculateCreateGroupModifications existingAdUsers sokratesTeachers sokratesStudents)
            ||> List.fold (fun list item -> item :: list)
        (existingUsers, modifications)

    let state =
        (state, existingAdUsers)
        ||> List.fold (fun (existingUsers, modifications) existingUser ->
            match calculateDeleteTeacherModification sokratesTeachers existingUser existingUsers with
            | Some (modification, existingUsers) -> (existingUsers, modification :: modifications)
            | None -> (existingUsers, modifications)
        )

    let state =
        (state, existingAdUsers)
        ||> List.fold (fun (existingUsers, modifications) existingUser ->
            match calculateDeleteStudentModification sokratesStudents existingUser existingUsers with
            | Some (modification, existingUsers) -> (existingUsers, modification :: modifications)
            | None -> (existingUsers, modifications)
        )

    let state =
        (state, sokratesTeachers)
        ||> List.fold (fun (existingUsers, modifications) sokratesTeacher ->
            let teacher = User.fromSokratesTeacherDto sokratesTeacher
            match calculateCreateTeacherModification existingUsers teacher (sokratesTeacher.DateOfBirth.ToString("dd.MM.yyyy")) with
            | Some (modification, existingUsers) -> (existingUsers, modification :: modifications)
            | None -> (existingUsers, modifications)
        )

    let state =
        (state, sokratesTeachers)
        ||> List.fold (fun (existingUsers, modifications) sokratesTeacher ->
            let teacher = User.fromSokratesTeacherDto sokratesTeacher
            match calculateTeacherSokratesIdUpdateModification existingUsers teacher with
            | Some (modification, existingUsers) -> (existingUsers, modification :: modifications)
            | None -> (existingUsers, modifications)
        )

    let state =
        (state, sokratesTeachers)
        ||> List.fold (fun (existingUsers, modifications) sokratesTeacher ->
            let teacher = User.fromSokratesTeacherDto sokratesTeacher
            match calculateChangeTeacherNameModification existingUsers teacher with
            | Some (modification, existingUsers) -> (existingUsers, modification :: modifications)
            | None -> (existingUsers, modifications)
        )

    let state =
        (state, sokratesStudents)
        ||> List.fold (fun (existingUsers, modifications) sokratesStudent ->
            let student =
                let rawUserName = userNameFromName sokratesStudent.FirstName1 sokratesStudent.LastName
                User.fromSokratesStudentDto sokratesStudent rawUserName // UserName is not important here, because we'll update it anyways
            match calculateCreateStudentModification existingUsers student (sokratesStudent.DateOfBirth.ToString("dd.MM.yyyy")) with
            | Some (modification, existingUsers) -> (existingUsers, modification :: modifications)
            | None -> (existingUsers, modifications)
        )

    let state =
        (state, sokratesStudents)
        ||> List.fold (fun (existingUsers, modifications) sokratesStudent ->
            let student =
                let rawUserName = userNameFromName sokratesStudent.FirstName1 sokratesStudent.LastName
                User.fromSokratesStudentDto sokratesStudent rawUserName // UserName is not important here, because we'll update it anyways
            match calculateChangeStudentNameModification existingUsers student with
            | Some (modification, existingUsers) -> (existingUsers, modification :: modifications)
            | None -> (existingUsers, modifications)
        )

    let state =
        (state, sokratesStudents)
        ||> List.fold (fun (existingUsers, modifications) sokratesStudent ->
            let student =
                let rawUserName = userNameFromName sokratesStudent.FirstName1 sokratesStudent.LastName
                User.fromSokratesStudentDto sokratesStudent rawUserName // UserName is not important here, because we're not going to use it
            match calculateMoveStudentToClassModifications existingUsers student with
            | Some (modification, existingUsers) -> (existingUsers, modification :: modifications)
            | None -> (existingUsers, modifications)
        )

    let state =
        let (existingUsers, modifications) = state
        let modifications =
            (modifications, calculateDeleteGroupModifications existingAdUsers sokratesTeachers sokratesStudents)
            ||> List.fold (fun list item -> item :: list)
        (existingUsers, modifications)

    let (_, modifications) = state in
    List.rev modifications

let getADModifications (adApi: AD.ADApi) (sokratesApi: Sokrates.SokratesApi) : HttpHandler =
    fun next ctx -> task {
        let! sokratesTeachers = sokratesApi.FetchTeachers |> Async.StartChild
        let timestamp =
            ctx.TryGetQueryStringValue "date"
            |> Option.map (fun date ->
                tryDo (fun () -> (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.CurrentCulture, DateTimeStyles.None))) ()
                |> Option.defaultWith (fun () -> failwithf "Can't parse \"%s\"" date)
            )
        let! sokratesStudents = sokratesApi.FetchStudents None timestamp |> Async.StartChild
        let adUsers = adApi.GetUsers () |> List.sortBy (fun v -> v.Type, v.LastName, v.FirstName)

        let! sokratesTeachers = sokratesTeachers |> Async.map (List.sortBy (fun v -> v.LastName, v.FirstName))
        let! sokratesStudents = sokratesStudents |> Async.map (List.sortBy (fun v -> v.SchoolClass, v.LastName, v.FirstName1))

        let modifications = modifications sokratesTeachers sokratesStudents adUsers
        return! Successful.OK modifications next ctx
    }

let verifyADModification (adApi: AD.ADApi) : HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<DirectoryModification>()
        match data with
        | CreateUser (user, [], password) ->
            let users =
                adApi.GetUsers ()
                |> List.map ExistingUser.fromADDto
            let mailAliases = uniqueMailAliases user users
            let modification = CreateUser (user, mailAliases, password)
            return! Successful.OK modification next ctx
        | _ -> return! RequestErrors.BAD_REQUEST "Can't verify modification" next ctx
    }

let applyADModifications (adApi: AD.ADApi) : HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<DirectoryModification list>()
        data
        |> List.map DirectoryModification.toADDto
        |> adApi.ApplyDirectoryModifications
        return! Successful.OK () next ctx
    }

let getADIncrementClassGroupUpdates (adApi: AD.ADApi) incrementClassGroupsConfig : HttpHandler =
    fun next ctx -> task {
        let classGroups =
            adApi.GetClassGroups ()
            |> List.map (ClassName.fromADDto >> (fun (ClassName groupName) -> groupName))

        let modifications = IncrementClassGroups.Core.modifications classGroups |> Reader.run incrementClassGroupsConfig
        return! Successful.OK modifications next ctx
    }

let applyADIncrementClassGroupUpdates (adApi: AD.ADApi) : HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<IncrementClassGroups.DataTransferTypes.ClassGroupModification list>()
        data
        |> List.map (ClassGroupModification.toDirectoryModification >> DirectoryModification.toADDto)
        |> adApi.ApplyDirectoryModifications
        return! Successful.OK () next ctx
    }
