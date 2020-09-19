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
    Address: Address option
    Phone1: string option
    Phone2: string option
    From: DateTimeOffset option
    Till: DateTimeOffset option
    UpdateDate: DateTimeOffset option
}

type StudentContactAddress = {
    Type: string
    Name: string
    EMailAddress: string option
    Address: Address option
    Phones: string list
    From: DateTimeOffset option
    Till: DateTimeOffset option
    UpdateDate: DateTimeOffset option
}

type StudentContact = {
    StudentId: SokratesId
    ContactAddresses: StudentContactAddress list
}
