module Domain

open Thoth.Json.Net

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
    UserName: string
    FirstName: string
    LastName: string
    MailAddresses: string list
}

module User =
    let encode u =
        Encode.object [
            "id", UserId.encode u.Id
            "userName", Encode.string u.UserName
            "firstName", Encode.string u.FirstName
            "lastName", Encode.string u.LastName
            "mailAddresses", (List.map Encode.string >> Encode.list) u.MailAddresses
        ]

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
