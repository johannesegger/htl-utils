module AADGroups

open Shared.AAD

type MemberUpdates =
    {
        AddMembers: UserId list
        RemoveMembers: UserId list
    }

module MemberUpdates =
    let toDto users memberUpdates =
        {
            Shared.AADGroups.MemberUpdates.AddMembers = memberUpdates.AddMembers |> List.map (flip Map.find users)
            Shared.AADGroups.MemberUpdates.RemoveMembers = memberUpdates.RemoveMembers |> List.map (flip Map.find users)
        }

    let fromDto (memberUpdates: Shared.AADGroups.MemberUpdates) =
        {
            AddMembers = memberUpdates.AddMembers |> List.map (fun m -> m.Id)
            RemoveMembers = memberUpdates.RemoveMembers |> List.map (fun m -> m.Id)
        }

type GroupUpdate =
    | CreateGroup of string * UserId list
    | UpdateGroup of GroupId * MemberUpdates
    | DeleteGroup of GroupId

module GroupUpdate =
    let toDto users groups = function
        | CreateGroup (groupName, memberIds) ->
            let members = memberIds |> List.map (flip Map.find users)
            Shared.AADGroups.CreateGroup (groupName, members)
        | UpdateGroup (groupId, memberUpdates) ->
            let group = groups |> Map.find groupId
            Shared.AADGroups.UpdateGroup (group, MemberUpdates.toDto users memberUpdates)
        | DeleteGroup groupId ->
            let group = groups |> Map.find groupId
            Shared.AADGroups.DeleteGroup group

    let fromDto = function
        | Shared.AADGroups.CreateGroup (name, members) ->
            CreateGroup (name, members |> List.map (fun m -> m.Id))
        | Shared.AADGroups.UpdateGroup (group, memberUpdates) ->
            UpdateGroup (group.Id, MemberUpdates.fromDto memberUpdates)
        | Shared.AADGroups.DeleteGroup group ->
            DeleteGroup group.Id

type AppliedGroupUpdate =
    | CreatedGroup of GroupId
    | UpdatedGroup of GroupId
    | DeletedGroup of GroupId

let calculateMemberUpdates teacherIds aadGroupMemberIds =
    {
        AddMembers =
            teacherIds
            |> List.except aadGroupMemberIds

        RemoveMembers =
            aadGroupMemberIds
            |> List.except teacherIds
    }

let calculateSingleGroupUpdates aadGroups groupName memberIds =
    aadGroups
    |> List.tryFind (fun (g: AAD.Group) -> String.equalsCaseInsensitive g.Name groupName)
    |> function
    | Some aadGroup ->
        let aadGroupMemberIds = aadGroup.Members |> List.map (fun m -> m.Id)
        let memberUpdates = calculateMemberUpdates memberIds aadGroupMemberIds
        if List.isEmpty memberUpdates.AddMembers && List.isEmpty memberUpdates.RemoveMembers
        then None
        else Some (UpdateGroup (aadGroup.Id, memberUpdates))
    | None ->
        Some (CreateGroup (groupName, memberIds))

let calculateAllGroupUpdates classesWithTeacherIds classTeacherIds allTeacherIds aadGroups =
    let getGroupName = Class.toString >> sprintf "GrpLehrer%s"
    [
        yield!
            classesWithTeacherIds
            |> List.choose (fun (``class``, teacherIds) ->
                calculateSingleGroupUpdates aadGroups (getGroupName ``class``) teacherIds
            )

        yield! calculateSingleGroupUpdates aadGroups "GrpKV" classTeacherIds |> Option.toList
        yield! calculateSingleGroupUpdates aadGroups "GrpLehrer" allTeacherIds |> Option.toList

        let desiredGroupNames =
            List.concat [
                classesWithTeacherIds |> List.map (fst >> getGroupName)
                [ "GrpKV"; "GrpLehrer" ]
            ]
        yield!
            aadGroups
            |> List.filter (fun aadGroup ->
                desiredGroupNames
                |> List.exists (fun groupName -> String.equalsCaseInsensitive groupName aadGroup.Name)
                |> not
            )
            |> List.map (fun g -> DeleteGroup g.Id)
    ]

let private lookupTeacherId aadUsers teacherShortName =
    aadUsers
    |> List.tryFind (fun (aadUser: AAD.User) ->
        String.equalsCaseInsensitive aadUser.ShortName teacherShortName
    )
    |> Option.map (fun u -> u.Id)

// TODO combine `classesWithTeachers`, `classTeachers` and `allTeachers` to a single list
let getGroupUpdates aadGroups aadUsers classesWithTeachers classTeachers allTeachers =
    let classesWithTeacherIds =
        classesWithTeachers
        |> Seq.map (Tuple.mapSnd (Seq.choose (lookupTeacherId aadUsers) >> Seq.toList))
        |> Seq.toList
    let classTeacherIds =
        classTeachers
        |> Map.toList
        |> List.map snd
        |> List.choose (lookupTeacherId aadUsers)
    let allTeacherIds =
        allTeachers
        |> List.choose (fun (t: Sokrates.Teacher) -> lookupTeacherId aadUsers t.ShortName)
    calculateAllGroupUpdates classesWithTeacherIds classTeacherIds allTeacherIds aadGroups

let applyGroupUpdate graphServiceClient update = async {
    match update with
    | CreateGroup (name, memberIds) ->
        let! group = AAD.createGroup graphServiceClient name
        let groupId = GroupId group.Id
        do! AAD.addMembersToGroup graphServiceClient groupId memberIds
        return CreatedGroup groupId
    | UpdateGroup (groupId, memberUpdates) ->
        do!
            [
                AAD.removeMembersFromGroup graphServiceClient groupId memberUpdates.RemoveMembers
                AAD.addMembersToGroup graphServiceClient groupId memberUpdates.AddMembers
            ]
            |> Async.Parallel
            |> Async.Ignore
        return UpdatedGroup groupId
    | DeleteGroup groupId ->
        do! AAD.deleteGroup graphServiceClient groupId
        return DeletedGroup groupId
}

let applyGroupUpdates graphServiceClient updates =
    updates
    |> List.map (applyGroupUpdate graphServiceClient)
    |> Async.Parallel
    |> Async.map Array.toList
