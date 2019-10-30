module FinalTheses

open Thoth.Json.Net

type Mentor = {
    MailAddress: string
}

module Mentor =
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                MailAddress = get.Required.Field "mailAddress" Decode.string
            }
        )
