module ADModifications.HttpHandler

open ADModifications.DataTransferTypes
open ADModifications.Mapping
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Thoth.Json.Net

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

let private modifications (sokratesTeachers: Sokrates.DataTransferTypes.Teacher list) (sokratesStudents: Sokrates.DataTransferTypes.Student list) (adUsers: AD.DataTransferTypes.User list) =
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
        |> List.choose (fun teacher ->
            let sokratesId = SokratesId.fromSokratesDto teacher.Id
            let userName = UserName teacher.ShortName
            match tryFindADTeacher sokratesId userName with
            | None ->
                let user = User.fromSokratesTeacherDto teacher
                Some (CreateUser (user, teacher.DateOfBirth.ToString("dd.MM.yyyy")))
            | Some adUser when adUser.FirstName <> teacher.FirstName || adUser.LastName <> teacher.LastName ->
                let user = { User.fromADDto adUser with SokratesId = Some sokratesId }
                let newUserName = UserName teacher.ShortName
                Some (UpdateUser (user, ChangeUserName (newUserName, teacher.FirstName, teacher.LastName)))
            | Some _ -> None
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
            | Some adUser when adUser.FirstName <> student.FirstName1 || adUser.LastName <> student.LastName ->
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
            | Some adUser when adUser.Type <> AD.DataTransferTypes.Student (AD.DataTransferTypes.GroupName student.SchoolClass) ->
                let user = User.fromADDto adUser
                let modification = UpdateUser (user, MoveStudentToClass (GroupName student.SchoolClass))
                (newUserNames, modification :: modifications)
            | Some _ -> (newUserNames, modifications)
        )
        |> snd
    let deleteUserModifications =
        adUsers
        |> List.choose (fun adUser ->
            let isInSokrates =
                match adUser.Type with
                | AD.DataTransferTypes.Teacher ->
                    adUser.SokratesId
                    |> Option.map (fun sokratesId -> Set.contains (SokratesId.fromADDto sokratesId) sokratesIds)
                    |> Option.defaultWith (fun () -> let (AD.DataTransferTypes.UserName userName) = adUser.Name in Set.contains userName sokratesTeacherNames)
                | AD.DataTransferTypes.Student _ ->
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
        let! sokratesTeachers = Http.get ctx (ServiceUrl.sokrates "teachers") (Decode.list Sokrates.DataTransferTypes.Teacher.decoder) |> Async.StartChild
        let! sokratesStudents = Http.get ctx (ServiceUrl.sokrates "students") (Decode.list Sokrates.DataTransferTypes.Student.decoder) |> Async.StartChild
        let! adUsers = Http.get ctx (ServiceUrl.ad "users") (Decode.list AD.DataTransferTypes.User.decoder) |> Async.StartChild

        let! sokratesTeachers = sokratesTeachers
        let! sokratesStudents = sokratesStudents
        let! adUsers = adUsers

        return!
            Ok modifications
            |> Result.apply (sokratesTeachers |> Result.mapError List.singleton)
            |> Result.apply (sokratesStudents |> Result.mapError List.singleton)
            |> Result.apply (adUsers |> Result.mapError List.singleton)
            |> function
            | Ok v -> Successful.OK v next ctx
            | Error e -> ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let applyADModifications : HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<DirectoryModification list>()
        let dto = data |> List.map DirectoryModification.toADDto
        let! result = Http.post ctx (ServiceUrl.ad "directory/modify") ((List.map AD.DataTransferTypes.DirectoryModification.encode >> Encode.list) dto) (Decode.nil ())
        match result with
        | Ok () -> return! Successful.OK () next ctx
        | Error e -> return! ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }
