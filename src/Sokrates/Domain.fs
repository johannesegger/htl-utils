module Sokrates.Domain

open System

type SokratesId = SokratesId of string

type Student = {
    Id: SokratesId
    LastName: string
    FirstName1: string
    FirstName2: string option
    DateOfBirth: DateTime
    SchoolClass: string
}

type Phone =
    | Home of string
    | Mobile of string

type Address = {
    Country: string
    Zip: string
    City: string
    Street: string
}

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

type StudentAddress = {
    StudentId: SokratesId
    Zip: string option
    City: string option
    Street: string option
    Phone1: string option
    Phone2: string option
    Country: string option
    From: DateTimeOffset option
    Till: DateTimeOffset option
    UpdateDate: DateTimeOffset option
}
