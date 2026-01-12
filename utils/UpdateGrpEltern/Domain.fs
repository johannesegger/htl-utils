module Domain

open System

type ParentGroupDefinition = {
    Name: string
    StudentsWithoutAddresses: string list
    StudentAddresses: string list
}

type UserType = MemberUser | GuestUser
type ExistingParentGroupMember = {
    UserId: string
    MailAddress: string
    UserType: UserType
}
type ExistingParentGroup = {
    GroupId: string
    Name: string
    Members: ExistingParentGroupMember list
}

let getParentsDiff parentGroupDefinitions existingParentGroups =
    let existingParents =
        existingParentGroups
        |> List.collect _.Members
        |> List.distinct
    let existingGuests =
        existingParents
        |> List.filter (fun v -> v.UserType = GuestUser)
    let newGuests =
        parentGroupDefinitions
        |> List.collect _.StudentAddresses
        |> List.filter (fun v -> not <| v.EndsWith($"@htlvb.at", StringComparison.InvariantCultureIgnoreCase))
        |> List.distinctBy CIString
    List.diff
        (existingGuests, _.MailAddress >> CIString)
        (newGuests, CIString)
