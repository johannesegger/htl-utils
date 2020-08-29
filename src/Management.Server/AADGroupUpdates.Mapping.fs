namespace AADGroupUpdates.Mapping

open AADGroupUpdates.DataTransferTypes

module UserId =
    let fromAADDto (AAD.Domain.UserId userId) = UserId userId
    let toAADDto (UserId userId) = AAD.Domain.UserId userId

module GroupId =
    let fromAADDto (AAD.Domain.GroupId groupId) = GroupId groupId
    let toAADDto (GroupId groupId) = AAD.Domain.GroupId groupId

module User =
    let fromAADDto (user: AAD.Domain.User) =
        {
            User.Id = UserId.fromAADDto user.Id
            User.FirstName = user.FirstName
            User.LastName = user.LastName
            User.ShortName = user.UserName
        }

module Group =
    let fromAADDto (group: AAD.Domain.Group) =
        {
            Group.Id = GroupId.fromAADDto group.Id
            Group.Name = group.Name
        }

module MemberModification =
    let toAADDto memberUpdates =
        [
            yield!
                memberUpdates.AddMembers
                |> List.map (fun user -> UserId.toAADDto user.Id |> AAD.Domain.AddMember)
            yield!
                memberUpdates.RemoveMembers
                |> List.map (fun user -> UserId.toAADDto user.Id |> AAD.Domain.RemoveMember)
        ]
    let toDto users memberUpdates =
        let addMembers =
            memberUpdates
            |> List.choose (function
                | AAD.Domain.AddMember userId ->
                    Some (Map.find userId users)
                | AAD.Domain.RemoveMember _ -> None
            )
        let removeMembers =
            memberUpdates
            |> List.choose (function
                | AAD.Domain.RemoveMember userId ->
                    Some (Map.find userId users)
                | AAD.Domain.AddMember _ -> None
            )
        {
            AddMembers = addMembers
            RemoveMembers = removeMembers
        }

module GroupModification =
    let fromAADDto users groups = function
        | AAD.Domain.CreateGroup (groupName, memberIds) ->
            let members = memberIds |> List.map (flip Map.find users)
            CreateGroup (groupName, members)
        | AAD.Domain.UpdateGroup (groupId, memberUpdates) ->
            let group = groups |> Map.find groupId
            UpdateGroup (group, MemberModification.toDto users memberUpdates)
        | AAD.Domain.DeleteGroup groupId ->
            let group = groups |> Map.find groupId
            DeleteGroup group
    let toAADDto = function
        | CreateGroup (name, users) ->
            AAD.Domain.CreateGroup (name, users |> List.map (fun user -> UserId.toAADDto user.Id))
        | UpdateGroup (group, memberUpdates) ->
            AAD.Domain.UpdateGroup (GroupId.toAADDto group.Id, MemberModification.toAADDto memberUpdates)
        | DeleteGroup group ->
            AAD.Domain.DeleteGroup (GroupId.toAADDto group.Id)
