module AADGroupUpdates

open AAD.DataTransferTypes

let calculateMemberUpdates teacherIds aadGroupMemberIds =
    [
        yield!
            teacherIds
            |> List.except aadGroupMemberIds
            |> List.map AddMember

        yield!
            aadGroupMemberIds
            |> List.except teacherIds
            |> List.map RemoveMember
    ]

let calculateAll aadGroups desiredGroups =
    [
        yield!
            desiredGroups
            |> List.choose (fun (groupName, userIds) ->
                aadGroups
                |> List.tryFind (fun aadGroup -> String.equalsCaseInsensitive groupName aadGroup.Name)
                |> function
                | Some aadGroup ->
                    match calculateMemberUpdates userIds aadGroup.Members with
                    | [] -> None
                    | memberUpdates -> UpdateGroup (aadGroup.Id, memberUpdates) |> Some
                | None ->
                    CreateGroup (groupName, userIds) |> Some
            )

        yield!
            aadGroups
            |> List.choose (fun aadGroup ->
                desiredGroups
                |> List.tryFind (fun (groupName, userIds) -> String.equalsCaseInsensitive groupName aadGroup.Name)
                |> function
                | Some _ -> None
                | None -> DeleteGroup aadGroup.Id |> Some
            )
    ]
