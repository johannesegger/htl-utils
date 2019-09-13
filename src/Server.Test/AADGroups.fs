module AADGroups

open Expecto
open Microsoft.Graph
open System
open System.Net
open System.Net.Http
open Shared.AADGroups

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
            let pacrId = "f7a4ad22-8a6a-478c-8266-175fa98d416e"
            let moljId = "bb336d83-addb-4098-8fe4-fbd3be61014e"
            let updates = [
                CreateGroup ("GrpEGGJTest", [ pacrId; moljId ])
            ]
            AADGroups.applyGroupUpdates graphServiceClient updates
            |> Async.map (
                List.exactlyOne
                >> function
                | CreatedGroup groupId -> groupId
                | UpdatedGroup _
                | DeletedGroup _ as x -> failwithf "Didn't expect %A." x
            )
        do!
            let eggjId = "498bc0fb-c2b5-4700-a6a2-f2f8cbe49b88"
            let updates = [
                UpdateGroup (groupId, [ AddMembers [ eggjId ] ])
            ]
            AADGroups.applyGroupUpdates graphServiceClient updates |> Async.Ignore
        do!
            let updates = [
                DeleteGroup groupId
            ]
            AADGroups.applyGroupUpdates graphServiceClient updates |> Async.Ignore
        return ()
    }
]