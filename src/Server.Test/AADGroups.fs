module AADGroups

open Expecto
open Microsoft.Graph
open Shared.AAD

let tests = testList "AADGroups" [
    ptestCaseAsync "Get group updates" <| async {
        // TODO implement
        // let! updates = AADGroups.getGroupUpdates aadGroups aadUsers classesWithTeachers classTeachers allTeachers
        ()
    }

    ptestCaseAsync "Create, update and delete group" <| async {
        let! authProvider = AAD.authProvider
        let graphServiceClient = GraphServiceClient(authProvider)
        let! groupId =
            let pacrId = UserId "f7a4ad22-8a6a-478c-8266-175fa98d416e"
            let moljId = UserId "bb336d83-addb-4098-8fe4-fbd3be61014e"
            let updates = [
                AADGroups.CreateGroup ("GrpEGGJTest", [ pacrId; moljId ])
            ]
            AADGroups.applyGroupUpdates graphServiceClient updates
            |> Async.map (
                List.exactlyOne
                >> function
                | AADGroups.CreatedGroup groupId -> groupId
                | AADGroups.UpdatedGroup _
                | AADGroups.DeletedGroup _ as x -> failwithf "Didn't expect %A." x
            )
        do!
            let eggjId = UserId "498bc0fb-c2b5-4700-a6a2-f2f8cbe49b88"
            let updates = [
                AADGroups.UpdateGroup (groupId, { AddMembers = [ eggjId ]; RemoveMembers = [] })
            ]
            AADGroups.applyGroupUpdates graphServiceClient updates |> Async.Ignore
        do!
            let updates = [
                AADGroups.DeleteGroup groupId
            ]
            AADGroups.applyGroupUpdates graphServiceClient updates |> Async.Ignore
        return ()
    }
]