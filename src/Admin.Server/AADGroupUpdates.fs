module AADGroupUpdates

open AAD

type MemberUpdates =
    {
        AddMembers: UserId list
        RemoveMembers: UserId list
    }

// module MemberUpdates =
//     let toDto users memberUpdates =
//         {
//             Shared.AADGroups.MemberUpdates.AddMembers = memberUpdates.AddMembers |> List.map (flip Map.find users)
//             Shared.AADGroups.MemberUpdates.RemoveMembers = memberUpdates.RemoveMembers |> List.map (flip Map.find users)
//         }

//     let fromDto (memberUpdates: Shared.AADGroups.MemberUpdates) =
//         {
//             AddMembers = memberUpdates.AddMembers |> List.map (fun m -> m.Id)
//             RemoveMembers = memberUpdates.RemoveMembers |> List.map (fun m -> m.Id)
//         }

type GroupUpdate =
    | CreateGroup of string * UserId list
    | UpdateGroup of GroupId * MemberUpdates
    | DeleteGroup of GroupId

// module GroupUpdate =
//     let toDto users groups = function
//         | CreateGroup (groupName, memberIds) ->
//             let members = memberIds |> List.map (flip Map.find users)
//             Shared.AADGroups.CreateGroup (groupName, members)
//         | UpdateGroup (groupId, memberUpdates) ->
//             let group = groups |> Map.find groupId
//             Shared.AADGroups.UpdateGroup (group, MemberUpdates.toDto users memberUpdates)
//         | DeleteGroup groupId ->
//             let group = groups |> Map.find groupId
//             Shared.AADGroups.DeleteGroup group

//     let fromDto = function
//         | Shared.AADGroups.CreateGroup (name, members) ->
//             CreateGroup (name, members |> List.map (fun m -> m.Id))
//         | Shared.AADGroups.UpdateGroup (group, memberUpdates) ->
//             UpdateGroup (group.Id, MemberUpdates.fromDto memberUpdates)
//         | Shared.AADGroups.DeleteGroup group ->
//             DeleteGroup group.Id

let calculateMemberUpdates teacherIds aadGroupMemberIds =
    let memberUpdates = {
        AddMembers =
            teacherIds
            |> List.except aadGroupMemberIds

        RemoveMembers =
            aadGroupMemberIds
            |> List.except teacherIds
    }
    match memberUpdates.AddMembers, memberUpdates.RemoveMembers with
    | [], [] -> None
    | _ -> Some memberUpdates

let calculateAll aadGroups desiredGroups =
    [
        yield!
            desiredGroups
            |> List.choose (fun (groupName, userIds) ->
                aadGroups
                |> List.tryFind (fun aadGroup -> String.equalsCaseInsensitive groupName aadGroup.Name)
                |> function
                | Some aadGroup ->
                    calculateMemberUpdates userIds aadGroup.Members
                    |> Option.map (fun memberUpdates -> UpdateGroup (aadGroup.Id, memberUpdates))
                | None ->
                    CreateGroup (groupName, userIds)
                    |> Some
            )

        yield!
            aadGroups
            |> List.choose (fun aadGroup ->
                desiredGroups
                |> List.tryFind (fun (groupName, userIds) -> String.equalsCaseInsensitive groupName aadGroup.Name)
                |> function
                | Some _ -> None
                | None -> DeleteGroup aadGroup.Id |> Some
            )
    ]
