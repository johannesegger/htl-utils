module AAD

open Thoth.Json.Net

type UserId = UserId of string

module UserId =
    let encode (UserId userId) = Encode.string userId
    let decoder : Decoder<_> = Decode.string |> Decode.map UserId
    let fromDto (Shared.AADGroupUpdates.UserId userId) = UserId userId
    let toDto (UserId groupId) = Shared.AADGroupUpdates.UserId groupId

type GroupId = GroupId of string

module GroupId =
    let encode (GroupId groupId) = Encode.string groupId
    let decoder : Decoder<_> = Decode.string |> Decode.map GroupId
    let fromDto (Shared.AADGroupUpdates.GroupId groupId) = GroupId groupId
    let toDto (GroupId groupId) = Shared.AADGroupUpdates.GroupId groupId

type User = {
    Id: UserId
    UserName: string
    FirstName: string
    LastName: string
    MailAddresses: string list
}

module User =
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Id = get.Required.Field "id" UserId.decoder
                UserName = get.Required.Field "userName" Decode.string
                FirstName = get.Required.Field "firstName" Decode.string
                LastName = get.Required.Field "lastName" Decode.string
                MailAddresses = get.Required.Field "mailAddresses" (Decode.list Decode.string)
            }
        )
    let toDto user =
        {
            Shared.AADGroupUpdates.User.Id = UserId.toDto user.Id
            Shared.AADGroupUpdates.User.FirstName = user.FirstName
            Shared.AADGroupUpdates.User.LastName = user.LastName
            Shared.AADGroupUpdates.User.ShortName = user.UserName
        }

type Group = {
    Id: GroupId
    Name: string
    Mail: string
    Members: UserId list
}

module Group =
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Id = get.Required.Field "id" GroupId.decoder
                Name = get.Required.Field "name" Decode.string
                Mail = get.Required.Field "mail" Decode.string
                Members = get.Required.Field "members" (Decode.list UserId.decoder)
            }
        )
    let toDto group =
        {
            Shared.AADGroupUpdates.Group.Id = GroupId.toDto group.Id
            Shared.AADGroupUpdates.Group.Name = group.Name
        }

type MemberModification =
    | AddMember of UserId
    | RemoveMember of UserId

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
    let encode = function
        | AddMember userId -> Encode.object [ "addMember", UserId.encode userId ]
        | RemoveMember userId -> Encode.object [ "removeMember", UserId.encode userId ]

type GroupModification =
    | CreateGroup of name: string * memberIds: UserId list
    | UpdateGroup of GroupId * MemberModification list
    | DeleteGroup of GroupId

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
    let encode = function
        | CreateGroup (name, memberIds) ->
            Encode.object [
                "createGroup", Encode.object [
                    "name", Encode.string name
                    "memberIds", (List.map UserId.encode >> Encode.list) memberIds
                ]
            ]
        | UpdateGroup (groupId, memberModifications) ->
            Encode.object [
                "updateGroup", Encode.object [
                    "groupId", GroupId.encode groupId
                    "memberModifications", (List.map MemberModification.encode >> Encode.list) memberModifications
                ]
            ]
        | DeleteGroup groupId ->
            Encode.object [ "deleteGroup", GroupId.encode groupId ]
