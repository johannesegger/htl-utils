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
    let encode (SokratesId sokratesId) = Encode.string sokratesId
    let decoder : Decoder<_> = Decode.string |> Decode.map SokratesId

type GroupName = GroupName of string
module GroupName =
    let encode (GroupName groupName) = Encode.string groupName
    let decoder : Decoder<_> = Decode.string |> Decode.map GroupName

type UserType = Teacher | Student of className: GroupName
module UserType =
    let encode = function
        | Teacher -> Encode.object [ "teacher", Encode.nil ]
        | Student className -> Encode.object [ "student", GroupName.encode className ]
    let decoder : Decoder<_> =
        Decode.oneOf [
            Decode.field "teacher" (Decode.nil Teacher)
            Decode.field "student" GroupName.decoder |> Decode.map Student
        ]

type User = {
    Name: UserName
    SokratesId: SokratesId option
    FirstName: string
    LastName: string
    Type: UserType
}
module User =
    let encode u =
        Encode.object [
            "name", UserName.encode u.Name
            "sokratesId", Encode.option SokratesId.encode u.SokratesId
            "firstName", Encode.string u.FirstName
            "lastName", Encode.string u.LastName
            "userType", UserType.encode u.Type
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Name = get.Required.Field "name" UserName.decoder
                SokratesId = get.Required.Field "sokratesId" (Decode.option SokratesId.decoder)
                FirstName = get.Required.Field "firstName" Decode.string
                LastName = get.Required.Field "lastName" Decode.string
                Type = get.Required.Field "userType" UserType.decoder
            }
        )

type UserUpdate =
    | ChangeUserName of UserName * firstName: string * lastName: string
    | SetSokratesId of SokratesId
    | MoveStudentToClass of GroupName

module UserUpdate =
    let encode = function
        | ChangeUserName (userName, firstName, lastName) -> Encode.object [ "changeName", Encode.tuple3 UserName.encode Encode.string Encode.string (userName, firstName, lastName) ]
        | SetSokratesId sokratesId -> Encode.object [ "setSokratesId", SokratesId.encode sokratesId ]
        | MoveStudentToClass newClassName -> Encode.object [ "moveStudentToClass", GroupName.encode newClassName ]
    let decoder : Decoder<_> =
        Decode.oneOf [
            Decode.field "changeName" (Decode.tuple3 UserName.decoder Decode.string Decode.string) |> Decode.map ChangeUserName
            Decode.field "setSokratesId" SokratesId.decoder |> Decode.map SetSokratesId
            Decode.field "moveStudentToClass" GroupName.decoder |> Decode.map MoveStudentToClass
        ]

type GroupUpdate =
    | ChangeGroupName of GroupName
module GroupUpdate =
    let encode = function
        | ChangeGroupName groupName -> Encode.object [ "changeName", GroupName.encode groupName ]
    let decoder : Decoder<_> =
        Decode.oneOf [
            Decode.field "changeName" GroupName.decoder |> Decode.map ChangeGroupName
        ]

type DirectoryModification =
    | CreateUser of User * password: string
    | UpdateUser of User * UserUpdate
    | DeleteUser of User
    | CreateGroup of UserType * UserName list
    | UpdateGroup of UserType * GroupUpdate
    | DeleteGroup of UserType
module DirectoryModification =
    let encode = function
        | CreateUser (user, password) -> Encode.object [ "createUser", Encode.tuple2 User.encode Encode.string (user, password) ]
        | UpdateUser (user, update) -> Encode.object [ "updateUser", Encode.tuple2 User.encode UserUpdate.encode (user, update) ]
        | DeleteUser user -> Encode.object [ "deleteUser", User.encode user ]
        | CreateGroup (userType, members) -> Encode.object [ "createGroup", Encode.tuple2 UserType.encode (List.map UserName.encode >> Encode.list) (userType, members) ]
        | UpdateGroup (userType, update) -> Encode.object [ "updateGroup", Encode.tuple2 UserType.encode GroupUpdate.encode (userType, update) ]
        | DeleteGroup userType -> Encode.object [ "deleteGroup", UserType.encode userType ]
    let decoder : Decoder<_> =
        Decode.oneOf [
            Decode.field "createUser" (Decode.tuple2 User.decoder Decode.string) |> Decode.map CreateUser
            Decode.field "updateUser" (Decode.tuple2 User.decoder UserUpdate.decoder) |> Decode.map UpdateUser
            Decode.field "deleteUser" User.decoder |> Decode.map DeleteUser
            Decode.field "createGroup" (Decode.tuple2 UserType.decoder (Decode.list UserName.decoder)) |> Decode.map CreateGroup
            Decode.field "updateGroup" (Decode.tuple2 UserType.decoder GroupUpdate.decoder) |> Decode.map UpdateGroup
            Decode.field "deleteGroup" UserType.decoder |> Decode.map DeleteGroup
        ]

type ClassGroupModification =
    | ChangeClassGroupName of oldName: GroupName * newName: GroupName
    | DeleteClassGroup of GroupName
module ClassGroupModification =
    let encode = function
        | ChangeClassGroupName (oldName, newName) -> Encode.object [ "changeClassGroupName", Encode.tuple2 GroupName.encode GroupName.encode (oldName, newName) ]
        | DeleteClassGroup groupName -> Encode.object [ "deleteClassGroup", GroupName.encode groupName ]
    let decoder : Decoder<_> =
        Decode.oneOf [
            Decode.field "changeClassGroupName" (Decode.tuple2 GroupName.decoder GroupName.decoder) |> Decode.map ChangeClassGroupName
            Decode.field "deleteClassGroup" GroupName.decoder |> Decode.map DeleteClassGroup
        ]

type ClassGroupModificationGroup = {
    Title: string
    Modifications: ClassGroupModification list
}
module ClassGroupModificationGroup =
    let encode g =
        Encode.object [
            "title", Encode.string g.Title
            "modifications", (List.map ClassGroupModification.encode >> Encode.list) g.Modifications
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Title = get.Required.Field "title" Decode.string
                Modifications = get.Required.Field "modifications" (Decode.list ClassGroupModification.decoder)
            }
        )
