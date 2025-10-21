module Domain

open System

type ParentGroupDefinition = {
    GroupName: string
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
    let existingGuests = existingParents |> List.filter (fun v -> v.UserType = GuestUser)
    let newGuests =
        parentGroupDefinitions
        |> List.collect _.StudentAddresses
        |> List.filter (fun v -> not <| v.EndsWith($"@htlvb.at", StringComparison.InvariantCultureIgnoreCase))
        |> List.map (fun v -> v.ToLowerInvariant())
        |> List.distinct
    List.diff
        (existingGuests, _.MailAddress.ToLowerInvariant())
        (newGuests, id)
