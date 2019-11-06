module Sokrates

open System
open System.Globalization
open Thoth.Json.Net

type SokratesId = SokratesId of string

type Student = {
    LastName: string
    FirstName: string
}
module Student =
    let decoder : Decoder<_> =
        Decode.object (fun get -> {
            LastName = get.Required.Field "lastName" Decode.string
            FirstName = get.Required.Field "firstName1" Decode.string
        })
    let toDto student =
        {
            Shared.Student.LastName = student.LastName
            Shared.Student.FirstName = student.FirstName
        }

type Phone =
    | Home of string
    | Mobile of string
module Phones =
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            [
                yield! get.Optional.Field "home" (Decode.list (Decode.string |> Decode.map Home)) |> Option.defaultValue []
                yield! get.Optional.Field "mobile" (Decode.list (Decode.string |> Decode.map Mobile)) |> Option.defaultValue []
            ]
        )

type Teacher = {
    LastName: string
    FirstName: string
    ShortName: string
    DateOfBirth: DateTime
    Phones: Phone list
}
module Teacher =
    let decoder : Decoder<_> =
        let tryDecodeDate v =
            match DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | (true, date) -> Decode.succeed date
            | (false, _) -> Decode.fail (sprintf "\"%s\" is not a valid date" v)
        Decode.object (fun get -> {
            LastName = get.Required.Field "lastName" Decode.string
            FirstName = get.Required.Field "firstName" Decode.string
            ShortName = get.Required.Field "shortName" Decode.string
            DateOfBirth = get.Required.Field "dateOfBirth" (Decode.string |> Decode.andThen tryDecodeDate)
            Phones = get.Required.Field "phones" Phones.decoder
        })

