module AADGroupUpdates.HttpHandler

open AADGroupUpdates.DataTransferTypes
open AADGroupUpdates.Mapping
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe

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

let getAADGroupUpdates adConfig (aadConfig: AAD.Configuration.Config) finalThesesConfig untisConfig : HttpHandler =
    fun next ctx -> task {
        let graphServiceClient = ctx.GetService<Microsoft.Graph.GraphServiceClient>()
        let teachingData = Untis.Core.getTeachingData |> Reader.run untisConfig
        let adUsers = AD.Core.getUsers |> Reader.run adConfig
        let finalThesesMentors = FinalTheses.Core.getMentors |> Reader.run finalThesesConfig
        let! aadUsers = async {
            let! users = AAD.Core.getUsers graphServiceClient
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
            let! predefinedGroups =
                AAD.Core.getPredefinedGroups graphServiceClient
                |> Reader.run aadConfig
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

        let studentsPerClass =
            adUsers
            |> List.choose (fun user ->
                match user.Type with
                | AD.Domain.Student (AD.Domain.GroupName className) ->
                    let (AD.Domain.UserName userName) = user.Name
                    match Map.tryFind userName aadUserLookupByUserName with
                    | Some aadUser -> Some (className, aadUser)
                    | None -> None
                | AD.Domain.Teacher -> None
            )
            |> List.groupBy fst
            |> List.map (Tuple.mapSnd (List.map snd))
            |> List.sortBy fst

        let classGroupsWithTeachers =
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
                (schoolClass, teacherIds)
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

        let teachersWithAnySubject subjects =
            teachingData
            |> List.choose (function
                | Untis.Domain.NormalTeacher (_, Untis.Domain.TeacherShortName teacherShortName, subject) ->
                    Some (teacherShortName, subject)
                | Untis.Domain.FormTeacher _
                | Untis.Domain.Custodian _
                | Untis.Domain.Informant _ -> None
            )
            |> List.filter (snd >> fun subject ->
                subjects |> List.map CIString |> List.contains (CIString subject.ShortName)
            )
            |> List.choose (fst >> flip Map.tryFind aadUserLookupByUserName)
            |> List.distinct

        let desiredGroups =
            aadConfig.PredefinedGroups
            |> Seq.collect (function
                | AAD.Configuration.Teachers groupName -> [ (groupName, teachers) ]
                | AAD.Configuration.FormTeachers groupName -> [ (groupName, formTeachers) ]
                | AAD.Configuration.FinalThesesMentors groupName -> [ (groupName, finalThesesMentors) ]
                | AAD.Configuration.ClassTeachers classNameToGroupName -> classGroupsWithTeachers |> List.map (Tuple.mapFst classNameToGroupName)
                | AAD.Configuration.ProfessionalGroup (groupName, subjects) -> [ (groupName, teachersWithAnySubject subjects) ]
                | AAD.Configuration.Students groupName -> [ (groupName, students) ]
                | AAD.Configuration.ClassStudents classNameToGroupName -> studentsPerClass |> List.map (Tuple.mapFst classNameToGroupName)
            )
            |> Seq.toList

        let updates = calculateAll aadPredefinedGroups desiredGroups

        return! Successful.OK updates next ctx
    }

let applyAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let graphServiceClient = ctx.GetService<Microsoft.Graph.GraphServiceClient>()
        let! input = ctx.BindJsonAsync<GroupUpdate list>()
        let modifications =
            input
            |> List.map GroupModification.toAADDto
        do! AAD.Core.applyGroupsModifications graphServiceClient modifications
        return! Successful.OK () next ctx
    }

let getAADIncrementClassGroupUpdates aadConfig incrementClassGroupsConfig : HttpHandler =
    fun next ctx -> task {
        let graphServiceClient = ctx.GetService<Microsoft.Graph.GraphServiceClient>()
        let! classGroups = async {
            let! predefinedGroups =
                AAD.Core.getPredefinedGroups graphServiceClient
                |> Reader.run aadConfig
            return
                predefinedGroups
                |> List.map (fun group -> group.Name)
        }

        let modifications = IncrementClassGroups.Core.modifications classGroups |> Reader.run incrementClassGroupsConfig
        return! Successful.OK modifications next ctx
    }

let applyAADIncrementClassGroupUpdates aadConfig : HttpHandler =
    fun next ctx -> task {
        let graphServiceClient = ctx.GetService<Microsoft.Graph.GraphServiceClient>()
        let! aadGroupNameLookupByName = async {
            let! predefinedGroups =
                AAD.Core.getPredefinedGroups graphServiceClient
                |> Reader.run aadConfig
            return
                predefinedGroups
                |> List.map (fun autoGroup -> autoGroup.Name, autoGroup.Id)
                |> Map.ofList
        }
        let! data = ctx.BindJsonAsync<IncrementClassGroups.DataTransferTypes.ClassGroupModification list>()
        let modifications =
            data
            |> List.map (ClassGroupModification.toAADGroupModification (flip Map.find aadGroupNameLookupByName))
        do! AAD.Core.applyGroupsModifications graphServiceClient modifications
        return! Successful.OK () next ctx
    }
