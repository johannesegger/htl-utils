module AAD.DataTransferTypes

open System
open Thoth.Json.Net

type UserId = UserId of string
module UserId =
    let encode (UserId userId) = Encode.string userId
    let decoder : Decoder<_> = Decode.string |> Decode.map UserId

type GroupId = GroupId of string
module GroupDistinguishedName =
    let encode (GroupId groupId) = Encode.string groupId
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

type Group = {
    Id: GroupId
    Name: string
    Mail: string
    Members: UserId list
}
module Group =
    let encode u =
        Encode.object [
            "id", GroupDistinguishedName.encode u.Id
            "name", Encode.string u.Name
            "mail", Encode.string u.Mail
            "members", (List.map UserId.encode >> Encode.list) u.Members
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Id = get.Required.Field "id" GroupDistinguishedName.decoder
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
                    "groupId", GroupDistinguishedName.encode groupId
                    "memberModifications", (List.map MemberModification.encode >> Encode.list) memberModifications
                ]
            ]
        | DeleteGroup groupId ->
            Encode.object [ "deleteGroup", GroupDistinguishedName.encode groupId ]
    let decoder : Decoder<_> =
        let createGroupDecoder : Decoder<_> =
            Decode.object (fun get ->
                let name = get.Required.Field "name" Decode.string
                let memberIds = get.Required.Field "memberIds" (Decode.list UserId.decoder)
                CreateGroup (name, memberIds)
            )
        let updateGroupDecoder : Decoder<_> =
            Decode.object (fun get ->
                let groupId = get.Required.Field "groupId" GroupDistinguishedName.decoder
                let memberModifications = get.Required.Field "memberModifications" (Decode.list MemberModification.decoder)
                UpdateGroup (groupId, memberModifications)
            )
        let deleteGroupDecoder : Decoder<_> =
            GroupDistinguishedName.decoder |> Decode.map DeleteGroup
        Decode.oneOf [
            Decode.field "createGroup" createGroupDecoder
            Decode.field "updateGroup" updateGroupDecoder
            Decode.field "deleteGroup" deleteGroupDecoder
        ]

type Base64EncodedImage = Base64EncodedImage of string
module Base64EncodedImage =
    let encode (Base64EncodedImage v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map Base64EncodedImage

type Contact = {
    FirstName: string
    LastName: string
    DisplayName: string
    Birthday: DateTime option
    HomePhones: string list
    MobilePhone: string option
    MailAddresses: string list
    Photo: Base64EncodedImage option
}
module Contact =
    let encode v =
        Encode.object [
            "firstName", Encode.string v.FirstName
            "lastName", Encode.string v.LastName
            "displayName", Encode.string v.DisplayName
            "birthday", Encode.option Encode.datetime v.Birthday
            "homePhones", (List.map Encode.string >> Encode.list) v.HomePhones
            "mobilePhone", Encode.option Encode.string v.MobilePhone
            "mailAddresses", (List.map Encode.string >> Encode.list) v.MailAddresses
            "photo", Encode.option Base64EncodedImage.encode v.Photo
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get -> {
            FirstName = get.Required.Field "firstName" Decode.string
            LastName = get.Required.Field "lastName" Decode.string
            DisplayName = get.Required.Field "displayName" Decode.string
            Birthday = get.Required.Field "birthday" (Decode.option Decode.datetime)
            HomePhones = get.Required.Field "homePhones" (Decode.list Decode.string)
            MobilePhone = get.Required.Field "mobilePhone" (Decode.option Decode.string)
            MailAddresses = get.Required.Field "mailAddresses" (Decode.list Decode.string)
            Photo = get.Required.Field "photo" (Decode.option Base64EncodedImage.decoder)
        })
