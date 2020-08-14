namespace AADGroupUpdates.Mapping

open AADGroupUpdates.DataTransferTypes

module UserId =
    let fromAADDto (AAD.DataTransferTypes.UserId userId) = UserId userId
    let toAADDto (UserId userId) = AAD.DataTransferTypes.UserId userId

module GroupId =
    let fromAADDto (AAD.DataTransferTypes.GroupId groupId) = GroupId groupId
    let toAADDto (GroupId groupId) = AAD.DataTransferTypes.GroupId groupId

module User =
    let fromAADDto (user: AAD.DataTransferTypes.User) =
        {
            User.Id = UserId.fromAADDto user.Id
            User.FirstName = user.FirstName
            User.LastName = user.LastName
            User.ShortName = user.UserName
        }

module Group =
    let fromAADDto (group: AAD.DataTransferTypes.Group) =
        {
            Group.Id = GroupId.fromAADDto group.Id
            Group.Name = group.Name
        }

module MemberModification =
    let toAADDto memberUpdates =
        [
            yield!
                memberUpdates.AddMembers
                |> List.map (fun user -> UserId.toAADDto user.Id |> AAD.DataTransferTypes.AddMember)
            yield!
                memberUpdates.RemoveMembers
                |> List.map (fun user -> UserId.toAADDto user.Id |> AAD.DataTransferTypes.RemoveMember)
        ]
    let toDto users memberUpdates =
        let addMembers =
            memberUpdates
            |> List.choose (function
                | AAD.DataTransferTypes.AddMember userId ->
                    Some (Map.find userId users)
                | AAD.DataTransferTypes.RemoveMember _ -> None
            )
        let removeMembers =
            memberUpdates
            |> List.choose (function
                | AAD.DataTransferTypes.RemoveMember userId ->
                    Some (Map.find userId users)
                | AAD.DataTransferTypes.AddMember _ -> None
            )
        {
            AddMembers = addMembers
            RemoveMembers = removeMembers
        }

module GroupModification =
    let fromAADDto users groups = function
        | AAD.DataTransferTypes.CreateGroup (groupName, memberIds) ->
            let members = memberIds |> List.map (flip Map.find users)
            CreateGroup (groupName, members)
        | AAD.DataTransferTypes.UpdateGroup (groupId, memberUpdates) ->
            let group = groups |> Map.find groupId
            UpdateGroup (group, MemberModification.toDto users memberUpdates)
        | AAD.DataTransferTypes.DeleteGroup groupId ->
            let group = groups |> Map.find groupId
            DeleteGroup group
    let toAADDto = function
        | CreateGroup (name, users) ->
            AAD.DataTransferTypes.CreateGroup (name, users |> List.map (fun user -> UserId.toAADDto user.Id))
        | UpdateGroup (group, memberUpdates) ->
            AAD.DataTransferTypes.UpdateGroup (GroupId.toAADDto group.Id, MemberModification.toAADDto memberUpdates)
        | DeleteGroup group ->
            AAD.DataTransferTypes.DeleteGroup (GroupId.toAADDto group.Id)
