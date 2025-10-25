module AD.Domain

open System

type UserName = UserName of string

type SokratesId = SokratesId of string

type GroupName = GroupName of string

type UserType = Teacher | Student of className: GroupName

type ProxyAddressProtocolType = SMTP

type ProxyAddressProtocol = {
    Type: ProxyAddressProtocolType
    IsPrimary: bool
}
module ProxyAddressProtocol =
    let tryParse v =
        if v = "SMTP" then Some { Type = SMTP; IsPrimary = true }
        elif v = "smtp" then Some { Type = SMTP; IsPrimary = false }
        else None
    let toString v =
        match v.Type, v.IsPrimary with
        | SMTP, true -> "SMTP"
        | SMTP, false -> "smtp"

type ProxyAddress =
    {
        Protocol: ProxyAddressProtocol
        Address: MailAddress
    }
module ProxyAddress =
    let tryParse (v: string) =
        match v.IndexOf(':') with
        | -1 -> None
        | idx ->
            match ProxyAddressProtocol.tryParse (v.Substring(0, idx)), MailAddress.tryParse (v.Substring(idx + 1)) with
            | Some protocol, Some address -> Some { Protocol = protocol; Address = address }
            | _ -> None
    let toString v =
        sprintf "%s:%s" (ProxyAddressProtocol.toString v.Protocol) (MailAddress.toString v.Address)

type MailAliasDomain = DefaultDomain | CustomDomain of string
module MailAliasDomain =
    let toString defaultDomain = function
        | DefaultDomain -> defaultDomain
        | CustomDomain v -> v

type MailAlias = {
    IsPrimary: bool
    UserName: string
    Domain: MailAliasDomain
}
module MailAlias =
    let toProxyAddress defaultDomain v =
        {
            Protocol = { IsPrimary = v.IsPrimary; Type = SMTP }
            Address = { UserName = v.UserName; Domain = MailAliasDomain.toString defaultDomain v.Domain }
        }

type NewUser = {
    Name: UserName
    SokratesId: SokratesId option
    FirstName: string
    LastName: string
    Type: UserType
    MailAliases: MailAlias list
    Password: string
}

type ExistingUser = {
    Name: UserName
    SokratesId: SokratesId option
    FirstName: string
    LastName: string
    Type: UserType
    CreatedAt: DateTime
    Mail: MailAddress option
    ProxyAddresses: ProxyAddress list
    UserPrincipalName: MailAddress
}

type UniqueUserAttributes = {
    UserNames: UserName list
    MailAddressUserNames: string list
}

type UserUpdate =
    | ChangeUserName of UserName * firstName: string * lastName: string * MailAlias list
    | SetSokratesId of SokratesId
    | MoveStudentToClass of GroupName

type GroupUpdate =
    | ChangeGroupName of GroupName

type DirectoryModification =
    | CreateUser of NewUser
    | UpdateUser of UserName * UserType * UserUpdate
    | DeleteUser of UserName * UserType
    | RestoreUser of UserName * UserType
    | CreateGroup of UserType
    | UpdateGroup of UserType * GroupUpdate
    | DeleteGroup of UserType

type NewGuestAccount = {
    UserName: UserName
    Password: string
    Notes: string option
}

type ExistingGuestAccount = {
    Name: UserName
    CreatedAt: DateTime
    WLANOnly: bool
    Notes: string option
}
