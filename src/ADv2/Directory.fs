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
    | Text of string
    | Bytes of byte[]
    | TextList of string list

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
