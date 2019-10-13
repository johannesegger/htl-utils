module Shared

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

type UserId = UserId of string

module UserId =
    let encode (UserId userId) = Encode.string userId
    let decoder : Decoder<_> = Decode.string |> Decode.map UserId

type GroupId = GroupId of string

module GroupId =
    let encode (GroupId userId) = Encode.string userId
    let decoder : Decoder<_> = Decode.string |> Decode.map GroupId

type User = {
    Id: UserId
    ShortName: string
    FirstName: string
    LastName: string
    MailAddresses: string list
}

module User =
    let encode u =
        Encode.object [
            "id", UserId.encode u.Id
            "shortName", Encode.string u.ShortName
            "firstName", Encode.string u.FirstName
            "lastName", Encode.string u.LastName
            "mailAddresses", (List.map Encode.string >> Encode.list) u.MailAddresses
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Id = get.Required.Field "id" UserId.decoder
                ShortName = get.Required.Field "shortName" Decode.string
                FirstName = get.Required.Field "firstName" Decode.string
                LastName = get.Required.Field "lastName" Decode.string
                MailAddresses = get.Required.Field "mailAddresses" (Decode.list Decode.string)
            }
        )

type Group = {
    Id: GroupId
    Name: string
    Mail: string
    Members: UserId list
}

module Group =
    let encode u =
        Encode.object [
            "id", GroupId.encode u.Id
            "name", Encode.string u.Name
            "mail", Encode.string u.Mail
            "members", (List.map UserId.encode >> Encode.list) u.Members
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Id = get.Required.Field "id" GroupId.decoder
                Name = get.Required.Field "name" Decode.string
                Mail = get.Required.Field "mail" Decode.string
                Members = get.Required.Field "members" (Decode.list UserId.decoder)
            }
        )

type MemberModification =
    | AddMember of UserId
    | RemoveMember of UserId

module MemberModification =
    let encode = function
        | AddMember userId -> Encode.object [ "addMember", UserId.encode userId ]
        | RemoveMember userId -> Encode.object [ "removeMember", UserId.encode userId ]
    let decoder : Decoder<_> =
        Decode.oneOf [
            Decode.field "addMember" UserId.decoder |> Decode.map AddMember
            Decode.field "removeMember" UserId.decoder |> Decode.map RemoveMember
        ]

type GroupModification =
    | CreateGroup of name: string * memberIds: UserId list
    | UpdateGroup of GroupId * MemberModification list
    | DeleteGroup of GroupId

module GroupModification =
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
    let decoder : Decoder<_> =
        let createGroupDecoder : Decoder<_> =
            Decode.object (fun get ->
                let name = get.Required.Field "name" Decode.string
                let memberIds = get.Required.Field "memberIds" (Decode.list UserId.decoder)
                CreateGroup (name, memberIds)
            )
        let updateGroupDecoder : Decoder<_> =
            Decode.object (fun get ->
                let groupId = get.Required.Field "groupId" GroupId.decoder
                let memberModifications = get.Required.Field "memberModifications" (Decode.list MemberModification.decoder)
                UpdateGroup (groupId, memberModifications)
            )
        let deleteGroupDecoder : Decoder<_> =
            GroupId.decoder |> Decode.map DeleteGroup
        Decode.oneOf [
            Decode.field "createGroup" createGroupDecoder
            Decode.field "updateGroup" updateGroupDecoder
            Decode.field "deleteGroup" deleteGroupDecoder
        ]
