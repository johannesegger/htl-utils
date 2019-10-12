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
