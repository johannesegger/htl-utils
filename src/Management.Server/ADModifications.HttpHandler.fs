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
    let fromADDto (adUser: AD.Domain.ExistingUser) =
        {
            User = User.fromADDto adUser
            MailAddressNames = [
                yield adUser.UserPrincipalName.UserName
                for v in adUser.ProxyAddresses -> v.Address.UserName
            ]
        }

type UniqueUserAttributes = {
    UserNames: UserName list
    MailAddressUserNames: string list
}
module UniqueUserAttributes =
    let fromADDto (v: AD.Domain.UniqueUserAttributes) =
        {
            UserNames = v.UserNames |> List.map UserName.fromADDto
            MailAddressUserNames = v.MailAddressUserNames
        }
    let merge v1 v2 =
        {
            UserNames = v1.UserNames @ v2.UserNames
            MailAddressUserNames = v1.MailAddressUserNames @ v2.MailAddressUserNames
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

let rawMailAliases (user: User) =
    [
        {
            IsPrimary = true
            UserName = $"%s{user.FirstName}.%s{user.LastName}" |> String.asAlphaNumeric
        }
    ]

let uniqueMailAliases user existingMailAddressUserNames =
    rawMailAliases user
    |> List.map (fun rawMailAliasName ->
        Seq.initInfinite (fun i -> i + 2)
        |> Seq.map string
        |> Seq.append [ "" ]
        |> Seq.map (fun number -> { rawMailAliasName with UserName = $"%s{rawMailAliasName.UserName}%s{number}" } )
        |> Seq.find (fun name -> not <| List.contains name.UserName existingMailAddressUserNames)
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

let getMailAddressNames (adUser: AD.Domain.ExistingUser) =
    [
        adUser.UserPrincipalName.UserName
        yield! adUser.ProxyAddresses |> List.map (fun v -> v.Address.UserName)
    ]

let calculateDeleteTeacherModification sokratesTeachers (existingUser: ExistingUser) =
    match existingUser.User.Type with
    | Teacher ->
        let sokratesUsers =
            sokratesTeachers
            |> List.map User.fromSokratesTeacherDto
        match tryFindUser sokratesUsers existingUser.User.SokratesId (Some existingUser.User.Name) with
        | None -> Some (DeleteUser existingUser.User)
        | Some _ -> None
    | _ -> None

let calculateDeleteStudentModification sokratesStudents (existingUser: ExistingUser) =
    match existingUser.User.Type with
    | Student _ ->
        let sokratesUsers =
            sokratesStudents
            |> List.map (fun (v: Sokrates.Student) ->
                let rawUserName = userNameFromName v.FirstName1 v.LastName
                User.fromSokratesStudentDto v rawUserName
            )
        match tryFindUser sokratesUsers existingUser.User.SokratesId None with
        | None -> Some (DeleteUser existingUser.User)
        | Some _ -> None
    | _ -> None

let calculateCreateTeacherModification users (teacher: User) password uniqueUserAttributes =
    match tryFindExistingUser users teacher.SokratesId (Some teacher.Name) with
    | Some _existingUser -> None
    | None ->
        let mailAliases = uniqueMailAliases teacher uniqueUserAttributes.MailAddressUserNames
        let modification = CreateUser (NewUser.fromUser teacher mailAliases password)
        let newUniqueUserAttributes = {
            UserNames = [ teacher.Name ]
            MailAddressUserNames = [
                let (UserName v) = teacher.Name in v // TODO assuming username becomes a mail address is not clean
                yield! mailAliases |> List.map (fun v -> v.UserName)
            ]
        }
        Some (modification, newUniqueUserAttributes)

let calculateCreateStudentModification users newUser =
    match tryFindExistingUser users newUser.SokratesId None with
    | Some _existingUser -> None
    | None ->
        let modification = CreateUser newUser
        let newUniqueUserAttributes = {
            UserNames = [ newUser.Name ]
            MailAddressUserNames = [
                let (UserName v) = newUser.Name in v // TODO assuming username becomes a mail address is not clean
                yield! newUser.MailAliases |> List.map (fun v -> v.UserName)
            ]
        }
        Some (modification, newUniqueUserAttributes)

let calculateTeacherSokratesIdUpdateModification users (teacher: User) =
    match tryFindExistingUser users teacher.SokratesId (Some teacher.Name), teacher.SokratesId with
    | Some existingUser, Some newSokratesId when existingUser.User.SokratesId <> Some newSokratesId ->
        Some (UpdateUser (existingUser.User, SetSokratesId newSokratesId))
    | _ -> None

let calculateChangeTeacherNameModification (users: ExistingUser list) (teacher: User) uniqueUserAttributes =
    match tryFindExistingUser users teacher.SokratesId (Some teacher.Name) with
    | Some user when user.User.Name <> teacher.Name || user.User.FirstName <> teacher.FirstName || user.User.LastName <> teacher.LastName ->
        let mailAliases =
            [
                yield! uniqueMailAliases teacher (uniqueUserAttributes.MailAddressUserNames |> List.except user.MailAddressNames)
                yield! user.MailAddressNames |> List.map (fun v -> { IsPrimary = false; UserName = v })
            ]
            |> List.distinctBy (fun v -> v.UserName)
        let modification = UpdateUser (user.User, ChangeUserName (teacher.Name, teacher.FirstName, teacher.LastName, mailAliases))
        let newUniqueUserAttributes = {
            UserNames = [ teacher.Name ]
            MailAddressUserNames = [
                let (UserName v) = teacher.Name in v // TODO assuming username becomes a mail address is not clean
                yield! mailAliases |> List.map (fun v -> v.UserName)
            ]
        }
        Some (modification, newUniqueUserAttributes)
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
        let existingMailAddressUserNames =
            users
            |> List.except [ user ]
            |> List.collect (fun v -> v.MailAddressNames)
        let mailAliases =
            [
                yield! uniqueMailAliases student existingMailAddressUserNames
                yield! user.MailAddressNames |> List.map (fun v -> { IsPrimary = false; UserName = v })
            ]
            |> List.distinctBy (fun v -> v.UserName)
        let modification = UpdateUser (user.User, ChangeUserName (student.Name, student.FirstName, student.LastName, mailAliases))
        let newUniqueUserAttributes = {
            UserNames = [ student.Name ]
            MailAddressUserNames = [
                let (UserName v) = student.Name in v // TODO assuming username becomes a mail address is not clean
                yield! mailAliases |> List.map (fun v -> v.UserName)
            ]
        }
        Some (modification, newUniqueUserAttributes)
    | _ -> None

let calculateMoveStudentToClassModifications users (student: User) =
    match tryFindExistingUser users student.SokratesId None, student.Type with
    | Some user, Student group when user.User.Type <> student.Type ->
        let modification = UpdateUser (user.User, MoveStudentToClass group)
        Some modification
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

let modifications sokratesTeachers sokratesStudents adUsers uniqueUserAttributes =
    let existingUsers =
        adUsers
        |> List.map ExistingUser.fromADDto
    let modifications = []
    let state = (uniqueUserAttributes, modifications)

    let state =
        let (uniqueUserAttributes, modifications) = state
        let modifications =
            (modifications, calculateCreateGroupModifications existingUsers sokratesTeachers sokratesStudents)
            ||> List.fold (fun list item -> item :: list)
        (uniqueUserAttributes, modifications)

    let state =
        let (uniqueUserAttributes, modifications) = state
        let newModifications =
            existingUsers
            |> List.choose (calculateDeleteTeacherModification sokratesTeachers)
        (uniqueUserAttributes, modifications @ newModifications)

    let state =
        let (uniqueUserAttributes, modifications) = state
        let newModifications =
            existingUsers
            |> List.choose (calculateDeleteStudentModification sokratesStudents)
        (uniqueUserAttributes, modifications @ newModifications)

    let state =
        (state, sokratesTeachers)
        ||> List.fold (fun (uniqueUserAttributes, modifications) sokratesTeacher ->
            let teacher = User.fromSokratesTeacherDto sokratesTeacher
            match calculateCreateTeacherModification existingUsers teacher (sokratesTeacher.DateOfBirth.ToString("dd.MM.yyyy")) uniqueUserAttributes with
            | Some (modification, newUniqueUserAttributes) ->
                let newUniqueUserAttributes = UniqueUserAttributes.merge uniqueUserAttributes newUniqueUserAttributes
                (newUniqueUserAttributes, modifications @ [ modification ])
            | None -> (uniqueUserAttributes, modifications)
        )

    let state =
        let (uniqueUserAttributes, modifications) = state
        let newModifications =
            sokratesTeachers
            |> List.choose (User.fromSokratesTeacherDto >> calculateTeacherSokratesIdUpdateModification existingUsers)
        (uniqueUserAttributes, modifications @ newModifications)

    let state =
        (state, sokratesTeachers)
        ||> List.fold (fun (uniqueUserAttributes, modifications) sokratesTeacher ->
            let teacher = User.fromSokratesTeacherDto sokratesTeacher
            match calculateChangeTeacherNameModification existingUsers teacher uniqueUserAttributes with
            | Some (modification, newUniqueUserAttributes) ->
                // TODO remove old username if changed
                let newUniqueUserAttributes = UniqueUserAttributes.merge uniqueUserAttributes newUniqueUserAttributes
                (newUniqueUserAttributes, modifications @ [ modification ])
            | None -> (uniqueUserAttributes, modifications)
        )

    let state =
        (state, sokratesStudents)
        ||> List.fold (fun (uniqueUserAttributes, modifications) sokratesStudent ->
            let userName =
                userNameFromName sokratesStudent.FirstName1 sokratesStudent.LastName
                |> uniqueUserName uniqueUserAttributes.UserNames
            let student = User.fromSokratesStudentDto sokratesStudent userName
            let mailAliases = uniqueMailAliases student uniqueUserAttributes.MailAddressUserNames
            let password = sokratesStudent.DateOfBirth.ToString("dd.MM.yyyy")
            let newUser = NewUser.fromUser student mailAliases password
            match calculateCreateStudentModification existingUsers newUser with
            | Some (modification: DirectoryModification, newUniqueUserAttributes) ->
                let newUniqueUserAttributes = UniqueUserAttributes.merge uniqueUserAttributes newUniqueUserAttributes
                (newUniqueUserAttributes, modifications @ [ modification ])
            | None -> (uniqueUserAttributes, modifications)
        )

    let state =
        (state, sokratesStudents)
        ||> List.fold (fun (uniqueUserAttributes, modifications) sokratesStudent ->
            let student =
                let rawUserName = userNameFromName sokratesStudent.FirstName1 sokratesStudent.LastName
                User.fromSokratesStudentDto sokratesStudent rawUserName // UserName is not important here, because we'll update it anyways
            match calculateChangeStudentNameModification existingUsers student with
            | Some (modification, existingUsers) -> (existingUsers, modifications @ [ modification ])
            | None -> (uniqueUserAttributes, modifications)
        )

    let state =
        (state, sokratesStudents)
        ||> List.fold (fun (uniqueUserAttributes, modifications) sokratesStudent ->
            let student =
                let rawUserName = userNameFromName sokratesStudent.FirstName1 sokratesStudent.LastName
                User.fromSokratesStudentDto sokratesStudent rawUserName // UserName is not important here, because we're not going to use it
            match calculateMoveStudentToClassModifications existingUsers student with
            | Some modification -> (uniqueUserAttributes, modifications @ [ modification ])
            | None -> (uniqueUserAttributes, modifications)
        )

    let state =
        let (uniqueUserAttributes, modifications) = state
        let newModifications = calculateDeleteGroupModifications existingUsers sokratesTeachers sokratesStudents
        (uniqueUserAttributes, modifications @ newModifications)

    let (_, modifications) = state
    modifications

let getADModifications (adApi: AD.Core.ADApi) (sokratesApi: Sokrates.SokratesApi) : HttpHandler =
    fun next ctx -> task {
        let! sokratesTeachers = sokratesApi.FetchTeachers |> Async.StartChild
        let timestamp =
            ctx.TryGetQueryStringValue "date"
            |> Option.map (fun date ->
                tryDo (fun () -> (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.CurrentCulture, DateTimeStyles.None))) ()
                |> Option.defaultWith (fun () -> failwithf "Can't parse \"%s\"" date)
            )
        let! sokratesStudents = sokratesApi.FetchStudents None timestamp |> Async.StartChild
        let! adUsers = adApi.GetUsers() |> Async.StartChild
        let! uniqueUserAttributes = adApi.GetAllUniqueUserProperties() |> Async.StartChild

        let! sokratesTeachers = sokratesTeachers |> Async.map (List.sortBy (fun v -> v.LastName, v.FirstName))
        let! sokratesStudents = sokratesStudents |> Async.map (List.sortBy (fun v -> v.SchoolClass, v.LastName, v.FirstName1))
        let! adUsers = adUsers |> Async.map (List.sortBy (fun v -> v.Type, v.LastName, v.FirstName))
        let! uniqueUserAttributes = uniqueUserAttributes |> Async.map UniqueUserAttributes.fromADDto

        let modifications = modifications sokratesTeachers sokratesStudents adUsers uniqueUserAttributes
        return! Successful.OK modifications next ctx
    }

let verifyADModification (adApi: AD.Core.ADApi) : HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<DirectoryModification>()
        match data with
        | CreateUser newUser ->
            let! users =
                adApi.GetUsers ()
                |> Async.map (List.map ExistingUser.fromADDto)
            let existingMailAddressUserNames = users |> List.collect (fun v -> v.MailAddressNames)
            let user = NewUser.toUser newUser
            let mailAliases = uniqueMailAliases user existingMailAddressUserNames
            let modification = CreateUser { newUser with MailAliases = mailAliases }
            return! Successful.OK modification next ctx
        | _ -> return! RequestErrors.BAD_REQUEST "Can't verify modification" next ctx
    }

let applyADModifications (adApi: AD.Core.ADApi) : HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<DirectoryModification list>()
        match! data |> List.map DirectoryModification.toADDto |> adApi.ApplyDirectoryModifications with
        | Ok () -> return! Successful.OK () next ctx
        | Error msgs -> return! ServerErrors.INTERNAL_ERROR (String.concat "\n" msgs) next ctx
    }

let getADIncrementClassGroupUpdates (adApi: AD.Core.ADApi) incrementClassGroupsConfig : HttpHandler =
    fun next ctx -> task {
        let! classGroups = adApi.GetClassGroups ()
        let classGroups = classGroups |> List.map (ClassName.fromADDto >> (fun (ClassName groupName) -> groupName))

        let modifications = IncrementClassGroups.Core.modifications classGroups |> Reader.run incrementClassGroupsConfig
        return! Successful.OK modifications next ctx
    }

let applyADIncrementClassGroupUpdates (adApi: AD.Core.ADApi) : HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<IncrementClassGroups.DataTransferTypes.ClassGroupModification list>()
        let modifications =
            data
            |> List.map (ClassGroupModification.toDirectoryModification >> DirectoryModification.toADDto)
        match! modifications |> adApi.ApplyDirectoryModifications with
        | Ok () -> return! Successful.OK () next ctx
        | Error msgs -> return! ServerErrors.INTERNAL_ERROR (String.concat "\n" msgs) next ctx
    }
