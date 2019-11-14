module Sokrates.DataTransferTypes

open System
open System.Globalization
open Thoth.Json.Net

module private Date =
    let decoder : Decoder<_> =
        Decode.string
        |> Decode.andThen (fun v ->
            match DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | (true, date) -> Decode.succeed date
            | (false, _) -> Decode.fail (sprintf "\"%s\" is not a valid date" v)
        )

type SokratesId = SokratesId of string
module SokratesId =
    let decoder : Decoder<_> = Decode.string |> Decode.map SokratesId

type Student = {
    Id: SokratesId
    LastName: string
    FirstName1: string
    FirstName2: string option
    DateOfBirth: DateTime
    SchoolClass: string
}
module Student =
    let decoder : Decoder<_> =
        Decode.object (fun get -> {
            Id = get.Required.Field "id" SokratesId.decoder
            LastName = get.Required.Field "lastName" Decode.string
            FirstName1 = get.Required.Field "firstName1" Decode.string
            FirstName2 = get.Optional.Field "firstName2" Decode.string
            DateOfBirth = get.Required.Field "dateOfBirth" Date.decoder
            SchoolClass = get.Required.Field "schoolClass" Decode.string
        })

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

type Address = {
    Country: string
    Zip: string
    City: string
    Street: string
}
module Address =
    let decoder : Decoder<_> =
        Decode.object (fun get -> {
            Country = get.Required.Field "country" Decode.string
            Zip = get.Required.Field "zip" Decode.string
            City = get.Required.Field "city" Decode.string
            Street = get.Required.Field "street" Decode.string
        })

type Teacher = {
    Id: SokratesId
    Title: string option
    LastName: string
    FirstName: string
    ShortName: string
    DateOfBirth: DateTime
    DegreeFront: string option
    DegreeBack: string option
    Phones: Phone list
    Address: Address option
}
module Teacher =
    let decoder : Decoder<_> =
        Decode.object (fun get -> {
            Id = get.Required.Field "id" SokratesId.decoder
            Title = get.Optional.Field "title" Decode.string
            LastName = get.Required.Field "lastName" Decode.string
            FirstName = get.Required.Field "firstName" Decode.string
            ShortName = get.Required.Field "shortName" Decode.string
            DateOfBirth = get.Required.Field "dateOfBirth" Date.decoder
            DegreeFront = get.Optional.Field "degreeFront" Decode.string
            DegreeBack = get.Optional.Field "degreeBack" Decode.string
            Phones = get.Optional.Field "phones" Phones.decoder |> Option.defaultValue []
            Address = get.Optional.Field "address" Address.decoder
        })

