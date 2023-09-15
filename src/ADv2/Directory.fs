module AD.Directory

type NodeType =
    | ADUser
    | ADGroup
    | ADOrganizationalUnit
    | ADComputer
module NodeType =
    let toString = function
        | ADUser -> "user"
        | ADGroup -> "group"
        | ADOrganizationalUnit -> "organizationalUnit"
        | ADComputer -> "computer"

type PropertyValue =
    | Unset
    | Text of string
    | Bytes of byte[]
    | TextList of string list

// see https://learn.microsoft.com/en-us/troubleshoot/windows-server/identity/useraccountcontrol-manipulate-account-properties#list-of-property-flags
module UserAccountControl =
    let ACCOUNTDISABLE = 0x2
    let PASSWD_NOTREQD = 0x20
    let NORMAL_ACCOUNT = 0x200
    let DONT_EXPIRE_PASSWORD = 0x10000

module AD =
    open System.Security.Principal
    open System.Text

    let password =
        sprintf "\"%s\""
        >> Encoding.Unicode.GetBytes

    let trySID data =
        try
            Some (SecurityIdentifier(data, 0))
        with _ -> None
