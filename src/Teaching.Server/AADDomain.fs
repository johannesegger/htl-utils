module AAD

open System
open Thoth.Json.Net

type UserId = UserId of string
module UserId =
    let encode (UserId userId) = Encode.string userId
    let decoder : Decoder<_> = Decode.string |> Decode.map UserId

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
