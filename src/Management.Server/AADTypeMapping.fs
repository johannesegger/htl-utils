module AADTypeMapping

open AAD.DataTransferTypes

module UserId =
    let fromDto (Shared.AADGroupUpdates.UserId userId) = UserId userId
    let toDto (UserId groupId) = Shared.AADGroupUpdates.UserId groupId

module GroupId =
    let fromDto (Shared.AADGroupUpdates.GroupId groupId) = GroupId groupId
    let toDto (GroupId groupId) = Shared.AADGroupUpdates.GroupId groupId

module User =
    let toDto (user: User) =
        {
            Shared.AADGroupUpdates.User.Id = UserId.toDto user.Id
            Shared.AADGroupUpdates.User.FirstName = user.FirstName
            Shared.AADGroupUpdates.User.LastName = user.LastName
            Shared.AADGroupUpdates.User.ShortName = user.UserName
        }

module Group =
    let toDto (group: Group) =
        {
            Shared.AADGroupUpdates.Group.Id = GroupId.toDto group.Id
            Shared.AADGroupUpdates.Group.Name = group.Name
        }

module MemberModification =
    let fromDto (memberUpdates: Shared.AADGroupUpdates.MemberUpdates) =
        [
            yield!
                memberUpdates.AddMembers
                |> List.map (fun user -> UserId.fromDto user.Id |> AddMember)
            yield!
                memberUpdates.RemoveMembers
                |> List.map (fun user -> UserId.fromDto user.Id |> RemoveMember)
        ]
    let toDto users memberUpdates =
        let addMembers =
            memberUpdates
            |> List.choose (function
                | AddMember userId ->
                    Some (Map.find userId users)
                | RemoveMember _ -> None
            )
        let removeMembers =
            memberUpdates
            |> List.choose (function
                | RemoveMember userId ->
                    Some (Map.find userId users)
                | AddMember _ -> None
            )
        {
            Shared.AADGroupUpdates.MemberUpdates.AddMembers = addMembers
            Shared.AADGroupUpdates.MemberUpdates.RemoveMembers = removeMembers
        }

module GroupModification =
    let fromDto = function
        | Shared.AADGroupUpdates.GroupUpdate.CreateGroup (name, users) ->
            CreateGroup (name, users |> List.map (fun user -> UserId.fromDto user.Id))
        | Shared.AADGroupUpdates.GroupUpdate.UpdateGroup (group, memberUpdates) ->
            UpdateGroup (GroupId.fromDto group.Id, MemberModification.fromDto memberUpdates)
        | Shared.AADGroupUpdates.GroupUpdate.DeleteGroup group ->
            DeleteGroup (GroupId.fromDto group.Id)
    let toDto users groups = function
        | CreateGroup (groupName, memberIds) ->
            let members = memberIds |> List.map (flip Map.find users)
            Shared.AADGroupUpdates.GroupUpdate.CreateGroup (groupName, members)
        | UpdateGroup (groupId, memberUpdates) ->
            let group = groups |> Map.find groupId
            Shared.AADGroupUpdates.GroupUpdate.UpdateGroup (group, MemberModification.toDto users memberUpdates)
        | DeleteGroup groupId ->
            let group = groups |> Map.find groupId
            Shared.AADGroupUpdates.GroupUpdate.DeleteGroup group
