module AADGroupUpdates.HttpHandler

open AADGroupUpdates.DataTransferTypes
open AADGroupUpdates.Mapping
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Thoth.Json.Net

let private calculateMemberUpdates teacherIds aadGroupMemberIds =
    [
        yield!
            teacherIds
            |> List.except aadGroupMemberIds
            |> List.map AAD.Domain.AddMember

        yield!
            aadGroupMemberIds
            |> List.except teacherIds
            |> List.map AAD.Domain.RemoveMember
    ]

let private calculateAll (aadGroups: AAD.Domain.Group list) desiredGroups =
    [
        yield!
            desiredGroups
            |> List.choose (fun (groupName, userIds) ->
                aadGroups
                |> List.tryFind (fun aadGroup -> CIString groupName = CIString aadGroup.Name)
                |> function
                | Some aadGroup ->
                    match calculateMemberUpdates userIds aadGroup.Members with
                    | [] -> None
                    | memberUpdates -> AAD.Domain.UpdateGroup (aadGroup.Id, memberUpdates) |> Some
                | None ->
                    AAD.Domain.CreateGroup (groupName, userIds) |> Some
            )

        yield!
            aadGroups
            |> List.choose (fun aadGroup ->
                desiredGroups
                |> List.tryFind (fun (groupName, userIds) -> CIString groupName = CIString aadGroup.Name)
                |> function
                | Some _ -> None
                | None -> AAD.Domain.DeleteGroup aadGroup.Id |> Some
            )
    ]

let getAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let teachingData = Untis.Core.getTeachingData ()
        let! sokratesTeachers = Sokrates.Core.getTeachers ()
        let finalThesesMentors = FinalTheses.Core.getMentors ()
        let! aadAutoGroups = AAD.Auth.withAuthTokenFromHttpContext ctx AAD.Core.getAutoGroups
        let! aadUsers = AAD.Auth.withAuthTokenFromHttpContext ctx AAD.Core.getUsers

        let updates =
            let aadUserLookupByUserName =
                aadUsers
                |> List.map (fun user -> user.UserName, user.Id)
                |> Map.ofList

            let teacherIds =
                sokratesTeachers
                |> List.choose (fun teacher -> Map.tryFind teacher.ShortName aadUserLookupByUserName)

            let classGroupsWithTeacherIds =
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

            let formTeacherIds =
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
                |> List.collect (fun user ->
                    user.MailAddresses
                    |> List.map (fun mailAddress -> CIString mailAddress, user.Id)
                )
                |> Map.ofList

            let finalThesesMentorIds =
                finalThesesMentors
                |> List.choose (fun m -> Map.tryFind (CIString m.MailAddress) aadUserLookupByMailAddress)

            let professionalGroupsWithTeacherIds =
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
                ("GrpLehrer", teacherIds)
                ("GrpKV", formTeacherIds)
                ("GrpDA-Betreuer", finalThesesMentorIds)
                yield! classGroupsWithTeacherIds
                yield! professionalGroupsWithTeacherIds
            ]

            let aadUserLookupById =
                aadUsers
                |> List.map (fun user -> user.Id, User.fromAADDto user)
                |> Map.ofList

            let aadAutoGroupsLookupById =
                aadAutoGroups
                |> List.map (fun group -> group.Id, Group.fromAADDto group)
                |> Map.ofList

            calculateAll aadAutoGroups desiredGroups
            |> List.map (GroupModification.fromAADDto aadUserLookupById aadAutoGroupsLookupById)

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
