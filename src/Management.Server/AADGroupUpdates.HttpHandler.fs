module AADGroupUpdates.HttpHandler

open AADGroupUpdates.DataTransferTypes
open AADGroupUpdates.Mapping
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open System
open System.Globalization

let private calculateMemberUpdates teacherIds aadGroupMemberIds =
    {
        AddMembers = teacherIds |> List.except aadGroupMemberIds
        RemoveMembers = aadGroupMemberIds |> List.except teacherIds
    }

let private calculateAll (actualGroups: (Group * User list) list) desiredGroups =
    [
        yield!
            desiredGroups
            |> List.choose (fun (groupName, userIds) ->
                actualGroups
                |> List.tryFind (fun (actualGroup, members) -> CIString groupName = CIString actualGroup.Name)
                |> function
                | Some (actualGroup, members) ->
                    let memberUpdates = calculateMemberUpdates userIds members
                    if MemberUpdates.isEmpty memberUpdates then None
                    else UpdateGroup (actualGroup, memberUpdates) |> Some
                | None ->
                    CreateGroup (groupName, userIds) |> Some
            )

        yield!
            actualGroups
            |> List.choose (fun (aadGroup, members) ->
                desiredGroups
                |> List.tryFind (fun (groupName, userIds) -> CIString groupName = CIString aadGroup.Name)
                |> function
                | Some _ -> None
                | None -> DeleteGroup aadGroup |> Some
            )
    ]

let predefinedGroupPrefix = Environment.getEnvVarOrFail "AAD_PREDEFINED_GROUP_PREFIX"

let getAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let teachingData = Untis.Core.getTeachingData ()
        let adUsers = AD.Core.getUsers ()
        let finalThesesMentors = FinalTheses.Core.getMentors ()
        let! aadUsers = async {
            let! users = AAD.Auth.withAuthTokenFromHttpContext ctx AAD.Core.getUsers
            return
                users
                |> List.map (fun user ->
                    User.fromAADDto user, user.MailAddresses
                )
        }
        let aadUserLookupById =
            aadUsers
            |> List.map (fun (user, mailAddresses) -> user.Id, user)
            |> Map.ofList
        let aadUserLookupByUserName =
            aadUsers
            |> List.map (fun (user, mailAddresses) -> user.UserName, user)
            |> Map.ofList
        let! aadPredefinedGroups = async {
            let! predefinedGroups = AAD.Auth.withAuthTokenFromHttpContext ctx (flip AAD.Core.getGroupsWithPrefix predefinedGroupPrefix)
            return
                predefinedGroups
                |> List.map (fun predefinedGroup ->
                    let group = Group.fromAADDto predefinedGroup
                    let users =
                        predefinedGroup.Members
                        |> List.map (UserId.fromAADDto >> flip Map.find aadUserLookupById)
                    group, users
                )
        }

        let teachers =
            adUsers
            |> List.choose (fun user ->
                match user.Type with
                | AD.Domain.Teacher ->
                    let (AD.Domain.UserName userName) = user.Name
                    Map.tryFind userName aadUserLookupByUserName
                | AD.Domain.Student _ -> None
            )

        let students =
            adUsers
            |> List.choose (fun user ->
                match user.Type with
                | AD.Domain.Student _ ->
                    let (AD.Domain.UserName userName) = user.Name
                    Map.tryFind userName aadUserLookupByUserName
                | AD.Domain.Teacher -> None
            )

        let classGroupsWithTeachers nameFn =
            teachingData
            |> List.choose (function
                | Untis.Domain.NormalTeacher (schoolClass, teacherShortName, _)
                | Untis.Domain.FormTeacher (schoolClass, teacherShortName) -> Some (schoolClass, teacherShortName)
                | Untis.Domain.Custodian _
                | Untis.Domain.Informant _ -> None
            )
            |> List.groupBy fst
            |> List.sortBy fst
            |> List.map (fun (Untis.Domain.SchoolClass schoolClass, teachers) ->
                let teacherIds =
                    teachers
                    |> List.choose (snd >> fun (Untis.Domain.TeacherShortName v) -> Map.tryFind v aadUserLookupByUserName)
                    |> List.distinct
                (nameFn schoolClass, teacherIds)
            )

        let formTeachers =
            teachingData
            |> List.choose (function
                | Untis.Domain.FormTeacher (_, Untis.Domain.TeacherShortName teacherShortName) -> Some teacherShortName
                | Untis.Domain.NormalTeacher _
                | Untis.Domain.Custodian _
                | Untis.Domain.Informant _ -> None
            )
            |> List.choose (flip Map.tryFind aadUserLookupByUserName)
            |> List.distinct

        let aadUserLookupByMailAddress =
            aadUsers
            |> List.collect (fun (user, mailAddresses) ->
                mailAddresses
                |> List.map (fun mailAddress -> CIString mailAddress, user)
            )
            |> Map.ofList

        let finalThesesMentors =
            finalThesesMentors
            |> List.choose (fun m -> Map.tryFind (CIString m.MailAddress) aadUserLookupByMailAddress)

        let professionalGroupsWithTeachers groupsWithSubjects =
            groupsWithSubjects
            |> Seq.map (Tuple.mapSnd (List.map CIString) >> fun (groupName, subjects) ->
                let teacherIds =
                    teachingData
                    |> List.choose (function
                        | Untis.Domain.NormalTeacher (_, Untis.Domain.TeacherShortName teacherShortName, subject) ->
                            Some (teacherShortName, subject)
                        | Untis.Domain.FormTeacher _
                        | Untis.Domain.Custodian _
                        | Untis.Domain.Informant _ -> None
                    )
                    |> List.filter (snd >> fun subject ->
                        subjects |> List.contains (CIString subject.ShortName)
                    )
                    |> List.choose (fst >> flip Map.tryFind aadUserLookupByUserName)
                    |> List.distinct
                (groupName, teacherIds)
            )

        let desiredGroups =
            Environment.getEnvVarOrFail "AAD_PREDEFINED_GROUPS"
            |> String.split ";"
            |> Seq.collect (fun row ->
                let rowParts = String.split "," row
                let groupId = Array.tryItem 0 rowParts |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of predefined groups settings: Can't get group id." row)
                let groupName = Array.tryItem 1 rowParts |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of predefined groups settings: Can't get group name." row)
                match groupId with
                | "Teachers" -> [ (groupName, teachers) ]
                | "FormTeachers" -> [ (groupName, formTeachers) ]
                | "FinalThesesMentors" -> [ (groupName, finalThesesMentors) ]
                | "ClassTeachers" -> classGroupsWithTeachers (fun name -> String.replace "<class>" name groupName)
                | "ProfessionalGroups" ->
                    Environment.getEnvVarOrFail "AAD_PROFESSIONAL_GROUPS_SUBJECTS"
                    |> String.split ";"
                    |> Seq.map (fun row ->
                        let (rawGroupName, subjectString) =
                            match row.IndexOf("-") with
                            | idx when idx >= 0 -> (row.Substring(0, idx), row.Substring(idx + 1))
                            | _ -> failwithf "Error in row \"%s\" of professional groups subjects settings: Can't find separator between group name and subjects" row
                        let fullGroupName = String.replace "<subject>" rawGroupName groupName
                        let subjects =
                            subjectString
                            |> String.split ","
                            |> List.ofArray
                        (fullGroupName, subjects)
                    )
                    |> professionalGroupsWithTeachers
                    |> Seq.toList
                | "Students" -> [ (groupName, students) ]
                | _ -> failwithf "Error in row \"%s\" of predefined groups settings: Unknown group id \"%s\"" row groupId
            )
            |> Seq.map (Tuple.mapFst (sprintf "%s%s" predefinedGroupPrefix))
            |> Seq.toList

        let updates = calculateAll aadPredefinedGroups desiredGroups

        return! Successful.OK updates next ctx
    }

let applyAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let! input = ctx.BindJsonAsync<GroupUpdate list>()
        let modifications =
            input
            |> List.map GroupModification.toAADDto
        do! AAD.Auth.withAuthTokenFromHttpContext ctx (flip AAD.Core.applyGroupsModifications modifications)
        return! Successful.OK () next ctx
    }

let getAADIncrementClassGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let! classGroups = async {
            let! predefinedGroups = AAD.Auth.withAuthTokenFromHttpContext ctx (flip AAD.Core.getGroupsWithPrefix predefinedGroupPrefix)
            return
                predefinedGroups
                |> List.map (fun group -> group.Name)
        }

        let modifications = IncrementClassGroups.Core.modifications classGroups
        return! Successful.OK modifications next ctx
    }

let applyAADIncrementClassGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let! aadGroupNameLookupByName = async {
            let! predefinedGroups = AAD.Auth.withAuthTokenFromHttpContext ctx (flip AAD.Core.getGroupsWithPrefix predefinedGroupPrefix)
            return
                predefinedGroups
                |> List.map (fun autoGroup -> autoGroup.Name, autoGroup.Id)
                |> Map.ofList
        }
        let! data = ctx.BindJsonAsync<IncrementClassGroups.DataTransferTypes.ClassGroupModification list>()
        let modifications =
            data
            |> List.map (ClassGroupModification.toAADGroupModification (flip Map.find aadGroupNameLookupByName))
        do! AAD.Auth.withAuthTokenFromHttpContext ctx (flip AAD.Core.applyGroupsModifications modifications)
        return! Successful.OK () next ctx
    }
