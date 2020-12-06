module AD.Domain

open System

type UserName = UserName of string

type SokratesId = SokratesId of string

type GroupName = GroupName of string

type UserType = Teacher | Student of className: GroupName

type NewUser = {
    Name: UserName
    SokratesId: SokratesId option
    FirstName: string
    LastName: string
    Type: UserType
}

type ExistingUser = {
    Name: UserName
    SokratesId: SokratesId option
    FirstName: string
    LastName: string
    Type: UserType
    CreatedAt: DateTime
    Mail: string option
    ProxyAddresses: string list
    UserPrincipalName: string
}

type UserUpdate =
    | ChangeUserName of UserName * firstName: string * lastName: string
    | SetSokratesId of SokratesId
    | MoveStudentToClass of GroupName

type GroupUpdate =
    | ChangeGroupName of GroupName

type DirectoryModification =
    | CreateUser of NewUser * password: string
    | UpdateUser of UserName * UserType * UserUpdate
    | DeleteUser of UserName * UserType
    | CreateGroup of UserType
    | UpdateGroup of UserType * GroupUpdate
    | DeleteGroup of UserType
