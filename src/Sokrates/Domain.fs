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
