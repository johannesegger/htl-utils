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
            |> List.map AAD.DataTransferTypes.AddMember

        yield!
            aadGroupMemberIds
            |> List.except teacherIds
            |> List.map AAD.DataTransferTypes.RemoveMember
    ]

let private calculateAll (aadGroups: AAD.DataTransferTypes.Group list) desiredGroups =
    [
        yield!
            desiredGroups
            |> List.choose (fun (groupName, userIds) ->
                aadGroups
                |> List.tryFind (fun aadGroup -> String.equalsCaseInsensitive groupName aadGroup.Name)
                |> function
                | Some aadGroup ->
                    match calculateMemberUpdates userIds aadGroup.Members with
                    | [] -> None
                    | memberUpdates -> AAD.DataTransferTypes.UpdateGroup (aadGroup.Id, memberUpdates) |> Some
                | None ->
                    AAD.DataTransferTypes.CreateGroup (groupName, userIds) |> Some
            )

        yield!
            aadGroups
            |> List.choose (fun aadGroup ->
                desiredGroups
                |> List.tryFind (fun (groupName, userIds) -> String.equalsCaseInsensitive groupName aadGroup.Name)
                |> function
                | Some _ -> None
                | None -> AAD.DataTransferTypes.DeleteGroup aadGroup.Id |> Some
            )
    ]

let getAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let! untisTeachingData = Http.get ctx (ServiceUrl.untis "teaching-data") (Decode.list Untis.DataTransferTypes.TeacherTask.decoder) |> Async.StartChild
        let! sokratesTeachers = Http.get ctx (ServiceUrl.sokrates "teachers") (Decode.list Sokrates.DataTransferTypes.Teacher.decoder) |> Async.StartChild
        let! finalThesesMentors = Http.get ctx (ServiceUrl.finalTheses "mentors") (Decode.list FinalTheses.DataTransferTypes.Mentor.decoder) |> Async.StartChild
        let! aadAutoGroups = Http.get ctx (ServiceUrl.aad "auto-groups") (Decode.list AAD.DataTransferTypes.Group.decoder) |> Async.StartChild
        let! aadUsers = Http.get ctx (ServiceUrl.aad "users") (Decode.list AAD.DataTransferTypes.User.decoder)

        let! untisTeachingData = untisTeachingData
        let! sokratesTeachers = sokratesTeachers
        let! finalThesesMentors = finalThesesMentors
        let! aadAutoGroups = aadAutoGroups

        let getUpdates aadUsers (aadAutoGroups: AAD.DataTransferTypes.Group list) sokratesTeachers teachingData finalThesesMentors =
            let aadUserLookupByUserName =
                aadUsers
                |> List.map (fun (user: AAD.DataTransferTypes.User) -> user.UserName, user.Id)
                |> Map.ofList

            let teacherIds =
                sokratesTeachers
                |> List.choose (fun (t: Sokrates.DataTransferTypes.Teacher) -> Map.tryFind t.ShortName aadUserLookupByUserName)

            let classGroupsWithTeacherIds =
                teachingData
                |> List.choose (function
                    | Untis.DataTransferTypes.NormalTeacher (schoolClass, teacherShortName, _)
                    | Untis.DataTransferTypes.FormTeacher (schoolClass, teacherShortName) -> Some (schoolClass, teacherShortName)
                    | Untis.DataTransferTypes.Custodian _
                    | Untis.DataTransferTypes.Informant _ -> None
                )
                |> List.groupBy fst
                |> List.map (fun (Untis.DataTransferTypes.SchoolClass schoolClass, teachers) ->
                    let teacherIds =
                        teachers
                        |> List.choose (snd >> fun (Untis.DataTransferTypes.TeacherShortName v) -> Map.tryFind v aadUserLookupByUserName)
                        |> List.distinct
                    (sprintf "GrpLehrer%s" schoolClass, teacherIds)
                )

            let formTeacherIds =
                teachingData
                |> List.choose (function
                    | Untis.DataTransferTypes.FormTeacher (_, Untis.DataTransferTypes.TeacherShortName teacherShortName) -> Some teacherShortName
                    | Untis.DataTransferTypes.NormalTeacher _
                    | Untis.DataTransferTypes.Custodian _
                    | Untis.DataTransferTypes.Informant _ -> None
                )
                |> List.choose (flip Map.tryFind aadUserLookupByUserName)
                |> List.distinct

            let aadUserLookupByMailAddress =
                aadUsers
                |> List.collect (fun (user: AAD.DataTransferTypes.User) ->
                    user.MailAddresses
                    |> List.map (fun mailAddress -> CIString mailAddress, user.Id)
                )
                |> Map.ofList

            let finalThesesMentorIds =
                finalThesesMentors
                |> List.choose (fun (m: FinalTheses.DataTransferTypes.Mentor) -> Map.tryFind (CIString m.MailAddress) aadUserLookupByMailAddress)

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
                            | Untis.DataTransferTypes.NormalTeacher (_, Untis.DataTransferTypes.TeacherShortName teacherShortName, subject) ->
                                Some (teacherShortName, subject)
                            | Untis.DataTransferTypes.FormTeacher _
                            | Untis.DataTransferTypes.Custodian _
                            | Untis.DataTransferTypes.Informant _ -> None
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
                |> List.map (fun (user: AAD.DataTransferTypes.User) -> user.Id, User.fromAADDto user)
                |> Map.ofList

            let aadAutoGroupsLookupById =
                aadAutoGroups
                |> List.map (fun group -> group.Id, Group.fromAADDto group)
                |> Map.ofList

            calculateAll aadAutoGroups desiredGroups
            |> List.map (GroupModification.fromAADDto aadUserLookupById aadAutoGroupsLookupById)

        return!
            Ok getUpdates
            |> Result.apply (Result.mapError List.singleton aadUsers)
            |> Result.apply (Result.mapError List.singleton aadAutoGroups)
            |> Result.apply (Result.mapError List.singleton sokratesTeachers)
            |> Result.apply (Result.mapError List.singleton untisTeachingData)
            |> Result.apply (Result.mapError List.singleton finalThesesMentors)
            |> function
            | Ok v -> Successful.OK v next ctx
            | Error e -> ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let applyAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let! input = ctx.BindJsonAsync<GroupUpdate list>()
        let body =
            input
            |> List.map (GroupModification.toAADDto >> AAD.DataTransferTypes.GroupModification.encode)
            |> Encode.list
        let! result = Http.post ctx (ServiceUrl.aad "auto-groups/modify") body (Decode.nil ())
        match result with
        | Ok () -> return! Successful.OK () next ctx
        | Error e -> return! ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }
