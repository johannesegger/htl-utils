module AD.Test.ConcurrentConnection

open AD.Configuration
open AD.Directory
open AD.Ldap
open AD.Test.Setup
open Expecto

let tests =
    testList "ConcurrentConnection" [
        testCaseTask "Concurrently set property" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINA,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap userDn ADUser []

            do!
                [1..100] |> List.map (fun i ->
                    System.Threading.Tasks.Task.Run(fun () ->
                        ldap.SetNodeProperties(userDn, [ "givenName", Text $"Albert %d{i}" ]) |> Async.RunSynchronously
                    )
                )
                |> System.Threading.Tasks.Task.WhenAll
                |> Async.AwaitTask
                |> Async.Ignore

            let! user = ldap.FindObjectByDn(userDn, [| "givenName" |])
            Expect.stringStarts (SearchResultEntry.getStringAttributeValue "givenName" user) "Albert" "Given name should be set by one concurrent operation"
        })
    ]
