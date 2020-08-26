module Sokrates.DataTransferTypes

open System
open System.Globalization
open Thoth.Json.Net

type SokratesId = SokratesId of string
module SokratesId =
    let encode (SokratesId v) = Encode.string v
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
    let encode v =
        Encode.object [
            "id", SokratesId.encode v.Id
            "lastName", Encode.string v.LastName
            "firstName1", Encode.string v.FirstName1
            "firstName2", Encode.option Encode.string v.FirstName2
            "dateOfBirth", Encode.datetime v.DateOfBirth
            "schoolClass", Encode.string v.SchoolClass
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get -> {
            Id = get.Required.Field "id" SokratesId.decoder
            LastName = get.Required.Field "lastName" Decode.string
            FirstName1 = get.Required.Field "firstName1" Decode.string
            FirstName2 = get.Required.Field "firstName2" (Decode.option Decode.string)
            DateOfBirth = get.Required.Field "dateOfBirth" Decode.datetime
            SchoolClass = get.Required.Field "schoolClass" Decode.string
        })

type Phone =
    | Home of string
    | Mobile of string
module Phone =
    let encode = function
        | Home v -> Encode.object [ "home", Encode.string v ]
        | Mobile v -> Encode.object [ "mobile", Encode.string v ]
    let decoder : Decoder<_> =
        Decode.oneOf [
            Decode.field "home" Decode.string |> Decode.map Home
            Decode.field "mobile" Decode.string |> Decode.map Mobile
        ]

type Address = {
    Country: string
    Zip: string
    City: string
    Street: string
}
module Address =
    let encode v =
        Encode.object [
            "country", Encode.string v.Country
            "zip", Encode.string v.Zip
            "city", Encode.string v.City
            "street", Encode.string v.Street
        ]
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
    let encode v = Encode.object [
        "id", SokratesId.encode v.Id
        "title", Encode.option Encode.string v.Title
        "lastName", Encode.string v.LastName
        "firstName", Encode.string v.FirstName
        "shortName", Encode.string v.ShortName
        "dateOfBirth", Encode.datetime v.DateOfBirth
        "degreeFront", Encode.option Encode.string v.DegreeFront
        "degreeBack", Encode.option Encode.string v.DegreeBack
        "phones", (List.map Phone.encode >> Encode.list) v.Phones
        "address", Encode.option Address.encode v.Address
    ]
    let decoder : Decoder<_> =
        Decode.object (fun get -> {
            Id = get.Required.Field "id" SokratesId.decoder
            Title = get.Required.Field "title" (Decode.option Decode.string)
            LastName = get.Required.Field "lastName" Decode.string
            FirstName = get.Required.Field "firstName" Decode.string
            ShortName = get.Required.Field "shortName" Decode.string
            DateOfBirth = get.Required.Field "dateOfBirth" Decode.datetime
            DegreeFront = get.Required.Field "degreeFront" (Decode.option Decode.string)
            DegreeBack = get.Required.Field "degreeBack" (Decode.option Decode.string)
            Phones = get.Required.Field "phones" (Decode.list Phone.decoder)
            Address = get.Required.Field "address" (Decode.option Address.decoder)
        })

