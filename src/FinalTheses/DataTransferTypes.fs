module FinalTheses.DataTransferTypes

open Thoth.Json.Net

type Mentor = {
    FirstName: string
    LastName: string
    MailAddress: string
}
module Mentor =
    let encode mentor =
        Encode.object [
            "firstName", Encode.string mentor.FirstName
            "lastName", Encode.string mentor.LastName
            "mailAddress", Encode.string mentor.MailAddress
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                FirstName = get.Required.Field "firstName" Decode.string
                LastName = get.Required.Field "lastName" Decode.string
                MailAddress = get.Required.Field "mailAddress" Decode.string
            }
        )
