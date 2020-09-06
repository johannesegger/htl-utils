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

let getAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let teachingData = Untis.Core.getTeachingData ()
        let! sokratesTeachers = Sokrates.Core.getTeachers ()
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
        let! aadAutoGroups = async {
            let! autoGroups = AAD.Auth.withAuthTokenFromHttpContext ctx AAD.Core.getAutoGroups
            return
                autoGroups
                |> List.map (fun autoGroup ->
                    let group = Group.fromAADDto autoGroup
                    let users =
                        autoGroup.Members
                        |> List.map (UserId.fromAADDto >> flip Map.find aadUserLookupById)
                    group, users
                )
        }

        let teachers =
            sokratesTeachers
            |> List.choose (fun teacher -> Map.tryFind teacher.ShortName aadUserLookupByUserName)

        let classGroupsWithTeachers =
            teachingData
            |> List.choose (function
                | Untis.Domain.NormalTeacher (schoolClass, teacherShortName, _)
                | Untis.Domain.FormTeacher (schoolClass, teacherShortName) -> Some (schoolClass, teacherShortName)
                | Untis.Domain.Custodian _
                | Untis.Domain.Informant _ -> None
            )
            |> List.groupBy fst
            |> List.map (fun (Untis.Domain.SchoolClass schoolClass, teachers) ->
                let teacherIds =
                    teachers
                    |> List.choose (snd >> fun (Untis.Domain.TeacherShortName v) -> Map.tryFind v aadUserLookupByUserName)
                    |> List.distinct
                (sprintf "GrpLehrer%s" schoolClass, teacherIds)
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

        let finalThesesMentorIds =
            finalThesesMentors
            |> List.choose (fun m -> Map.tryFind (CIString m.MailAddress) aadUserLookupByMailAddress)

        let professionalGroupsWithTeachers =
            [
                "GrpD", [ "D" ]
                "GrpE", [ "E1" ]
                "GrpAM", [ "AM" ]
                "GrpCAD", [ "KOBE"; "KOP1"; "MT"; "PLP" ]
                "GrpWE", [ "ETAUTWP_4"; "FET1WP_3"; "FET1WP_4"; "WLA"; "WPT_3"; "WPT_4" ]
            ]
            |> List.map (Tuple.mapSnd (List.map CIString) >> fun (groupName, subjects) ->
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

        let desiredGroups = [
            ("GrpLehrer", teachers)
            ("GrpKV", formTeachers)
            ("GrpDA-Betreuer", finalThesesMentorIds)
            yield! classGroupsWithTeachers
            yield! professionalGroupsWithTeachers
        ]

        let updates = calculateAll aadAutoGroups desiredGroups

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
            let! aadGroups = AAD.Auth.withAuthTokenFromHttpContext ctx AAD.Core.getAutoGroups
            return
                aadGroups
                |> List.map (fun group -> group.Name)
        }

        let modifications = IncrementClassGroups.Core.modifications classGroups
        return! Successful.OK modifications next ctx
    }

let applyAADIncrementClassGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let! aadGroupNameLookupByName = async {
            let! autoGroups = AAD.Auth.withAuthTokenFromHttpContext ctx AAD.Core.getAutoGroups
            return
                autoGroups
                |> List.map (fun autoGroup -> autoGroup.Name, autoGroup.Id)
                |> Map.ofList
        }
        let! data = ctx.BindJsonAsync<IncrementClassGroups.DataTransferTypes.ClassGroupModification list>()
        let modifications =
            data
            |> List.map (ClassGroupModification.toAADGroupModification (flip Map.find aadGroupNameLookupByName))
        // do! AAD.Auth.withAuthTokenFromHttpContext ctx (flip AAD.Core.applyGroupsModifications modifications)
        printfn "Modifications: %A" modifications
        return! Successful.OK () next ctx
    }
