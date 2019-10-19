module AAD

open Thoth.Json.Net

type UserId = UserId of string

module UserId =
    let decoder : Decoder<_> = Decode.string |> Decode.map UserId

type GroupId = GroupId of string

module GroupId =
    let decoder : Decoder<_> = Decode.string |> Decode.map GroupId

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
