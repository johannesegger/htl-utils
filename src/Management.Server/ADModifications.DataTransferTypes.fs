namespace ADModifications.DataTransferTypes

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

type UserName = UserName of string
module UserName =
    let encode (UserName v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map UserName

type SokratesId = SokratesId of string
module SokratesId =
    let encode (SokratesId v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map SokratesId

type ClassName = ClassName of string
module ClassName =
    let encode (ClassName v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map ClassName

type UserType = Teacher | Student of ClassName

type User = {
    Name: UserName
    SokratesId: SokratesId option
    FirstName: string
    LastName: string
    Type: UserType
}

type MailAliasDomain = DefaultDomain | CustomDomain of string

type MailAlias = {
    IsPrimary: bool
    UserName: string
    Domain: MailAliasDomain
}
module MailAlias =
    let toNonPrimary v =
        { v with IsPrimary = false }

type UserUpdate =
    | ChangeUserName of UserName * firstName: string * lastName: string * MailAlias list
    | SetSokratesId of SokratesId
    | MoveStudentToClass of ClassName

type StudentClassUpdate =
    | ChangeStudentClassName of ClassName

type DirectoryModification =
    | CreateUser of User * MailAlias list * password: string
    | UpdateUser of User * UserUpdate
    | DeleteUser of User
    | CreateGroup of UserType
    | UpdateStudentClass of ClassName * StudentClassUpdate
    | DeleteGroup of UserType

module Thoth =
    let addCoders =
        Extra.withCustom UserName.encode UserName.decoder
        >> Extra.withCustom SokratesId.encode SokratesId.decoder
        >> Extra.withCustom ClassName.encode ClassName.decoder
