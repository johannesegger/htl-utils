module AAD.Domain

open System

type UserId = UserId of string

type User = {
    Id: UserId
    UserName: string
    FirstName: string
    LastName: string
    MailAddresses: string list
}

type GroupId = GroupId of string

type Group = {
    Id: GroupId
    Name: string
    Mail: string
    Members: UserId list
}

type MemberModification =
    | AddMember of UserId
    | RemoveMember of UserId

type GroupModification =
    | CreateGroup of name: string * memberIds: UserId list
    | UpdateGroup of GroupId * MemberModification list
    | DeleteGroup of GroupId

type Base64EncodedImage = Base64EncodedImage of string

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
