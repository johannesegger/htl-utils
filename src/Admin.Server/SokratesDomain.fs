module Sokrates

open System
open Thoth.Json.Net

type Teacher = {
    LastName: string
    FirstName: string
    ShortName: string
}

module Teacher =
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            match get.Optional.Field "shortName" Decode.string with
            | Some shortName ->
                {
                    LastName = get.Required.Field "lastName" Decode.string
                    FirstName = get.Required.Field "firstName" Decode.string
                    ShortName = shortName
                }
                |> Some
            | None -> None
        )
