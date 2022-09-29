namespace AADGroupUpdates.Mapping

open AADGroupUpdates.DataTransferTypes

type SokratesId = SokratesId of string
module SokratesId =
    let fromSokratesDto (Sokrates.SokratesId v) = SokratesId v
    let fromADDto (AD.SokratesId v) = SokratesId v

module UserId =
    let fromAADDto (AAD.Domain.UserId userId) = UserId userId
    let toAADDto (UserId userId) = AAD.Domain.UserId userId

module GroupId =
    let fromAADDto (AAD.Domain.GroupId groupId) = GroupId groupId
    let toAADDto (GroupId groupId) = AAD.Domain.GroupId groupId

module User =
    let fromAADDto (user: AAD.Domain.User) =
        {
            Id = UserId.fromAADDto user.Id
            FirstName = user.FirstName
            LastName = user.LastName
            UserName = user.UserName
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

module GroupModification =
    let toAADDto = function
        | CreateGroup (name, users) ->
            AAD.Domain.CreateGroup (name, users |> List.map (fun user -> UserId.toAADDto user.Id))
        | UpdateGroup (group, memberUpdates) ->
            AAD.Domain.UpdateGroup (GroupId.toAADDto group.Id, MemberModification.toAADDto memberUpdates)
        | DeleteGroup group ->
            AAD.Domain.DeleteGroup (GroupId.toAADDto group.Id)

module ClassGroupModification =
    let toAADGroupModification lookupGroupId = function
        | IncrementClassGroups.DataTransferTypes.ChangeClassGroupName (oldName, newName) ->
            AAD.Domain.ChangeGroupName (lookupGroupId oldName, newName)
        | IncrementClassGroups.DataTransferTypes.DeleteClassGroup name ->
            AAD.Domain.DeleteGroup (lookupGroupId name)
