module AD.Test.Ldap

open AD
open AD.Configuration
open AD.Directory
open AD.Ldap
open AD.Test.Setup
open Expecto
open System
open System.Text.RegularExpressions

type TemporaryGroup(dn: DistinguishedName, disposable: IAsyncDisposable) =
    member this.Dn = dn
    interface IAsyncDisposable with member _.DisposeAsync() = disposable.DisposeAsync()

let private createTemporaryGroup ldap name = task {
    let members =
        [1..3]
        |> List.map (fun i -> DistinguishedName $"CN=%s{name}%d{i},CN=Users,DC=htlvb,DC=intern")
    let! createdMemberNodes =
        members
        |> List.map (fun memberDn -> createNodeAndParents ldap memberDn ADUser [])
        |> Async.Sequential
        |> Async.map (Seq.rev >> AsyncDisposable.combine)
    let membersProperty =
        [ for DistinguishedName dn in members -> dn ]
        |> TextList
    let groupDn = DistinguishedName $"CN=%s{name},CN=Users,DC=htlvb,DC=intern"
    let! createdGroupNodes = createNodeAndParents ldap groupDn ADGroup ["member", membersProperty ]
    return TemporaryGroup(groupDn, AsyncDisposable.combine [ createdGroupNodes; createdMemberNodes ])
}

let private userPassword = "Test123"
let private createUser ldap userDn properties = async {
    return! createNodeAndParents ldap userDn ADUser [
        yield! properties
        yield! DN.tryCN userDn |> Option.map (fun userName -> "sAMAccountName", Text userName) |> Option.toList
        ("userAccountControl", Text $"{UserAccountControl.NORMAL_ACCOUNT ||| UserAccountControl.DONT_EXPIRE_PASSWORD ||| UserAccountControl.PASSWD_NOTREQD}")
        ("unicodePwd", Bytes (AD.password userPassword))
    ]
}

let tests =
    testList "Ldap" [
        testCaseTask "Create node" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINA,CN=Users,DC=htlvb,DC=intern"

            use! __ = createNodeAndParents ldap userDn ADUser []
            let! node = ldap.FindObjectByDn(userDn, [||])

            Expect.isNotNull node "Node should be found after creation"
        })

        testCaseTask "Find non-existing node" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINB,CN=Users,DC=htlvb,DC=intern"

            let! findResult = ldap.FindObjectByDn(userDn, [||]) |> Async.Catch

            Expect.isChoice2Of2 findResult "Node should not be found"
        })

        testCaseTask "Create node twice" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINC,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap userDn ADUser []

            let! createdNodes = ldap.CreateNodeAndParents(userDn, ADUser, [])

            Expect.isEmpty createdNodes "Creating node twice should be no-op"
        })

        testCaseTask "Create node and parent OUs" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EIND,OU=Lehrer,OU=Benutzer,OU=HTL,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap userDn ADUser []
            let! node = ldap.FindObjectByDn(userDn, [||])

            Expect.isNotNull node "Node should be found after creation"
        })

        testCaseTask "Delete node" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINE,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap userDn ADUser []

            do! ldap.DeleteNode(userDn)
            let! findResult = ldap.FindObjectByDn(userDn, [||]) |> Async.Catch

            Expect.isChoice2Of2 findResult "Node should not be found after deletion"
        })

        testCaseTask "Find group members" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            use! group = createTemporaryGroup ldap "EINF"

            let! actualMembers = ldap.FindGroupMembersIfGroupExists(group.Dn)

            Expect.isNonEmpty actualMembers "Member list should not be empty"
            Expect.all actualMembers (fun (DistinguishedName v) -> not <| String.IsNullOrEmpty v) "Group members should be stored"
        })

        testCaseTask "Find group members when group doesn't exist" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)

            let! actualMembers = ldap.FindGroupMembersIfGroupExists(DistinguishedName "CN=NoGroup,OU=HTLVB-Groups,DC=htlvb,DC=intern")

            Expect.isEmpty actualMembers "Member list is not empty"
        })

        testCaseTask "Find full group members" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            use! temporarySuperGroup = createTemporaryGroup ldap "EINGParent"
            use! temporarySubGroup = createTemporaryGroup ldap "EINGChild"
            do! ldap.AddObjectToGroup(temporarySuperGroup.Dn, temporarySubGroup.Dn)
            let! superGroup = ldap.FindObjectByDn(temporarySuperGroup.Dn, [| "member" |])

            let! members = ldap.FindFullGroupMembers(temporarySuperGroup.Dn, [| "objectSid" |])

            let actual =
                members
                |> List.map (fun v -> v.DistinguishedName, SearchResultEntry.getBytesAttributeValue "objectSid" v)
                |> Set.ofList
            let! expected =
                SearchResultEntry.getStringAttributeValues "member" superGroup
                |> List.map (DistinguishedName >> fun childDn -> ldap.FindObjectByDn(childDn, [| "objectSid" |]))
                |> List.append [ ldap.FindObjectByDn(temporarySubGroup.Dn, [| "objectSid" |]) ]
                |> Async.Parallel
                |> Async.map (Seq.map (fun v -> (v.DistinguishedName, SearchResultEntry.getBytesAttributeValue "objectSid" v)) >> Set.ofSeq)
            Expect.equal actual expected "Group should have members"
        })

        testCaseTask "Find descendant users" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let users =
                [1..3]
                |> List.map (fun i -> DistinguishedName $"CN=EINH%d{i},OU=3AHWII,OU=Students,DC=htlvb,DC=intern")
            use! __ =
                users
                |> List.map (fun userDn -> async { return! createNodeAndParents ldap userDn ADUser [] })
                |> Async.Sequential
                |> Async.map (Seq.rev >> AsyncDisposable.combine)

            let! nodes = ldap.FindDescendantUsers(DistinguishedName "OU=Students,DC=htlvb,DC=intern", [||])
            let nodeDns = nodes |> List.map (fun v -> DistinguishedName v.DistinguishedName)

            Expect.equal (Set.ofList nodeDns) (Set.ofList users) "Should find all users"
        })

        testCaseTask "Find descendant computers" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let computers =
                [1..3]
                |> List.map (fun i -> DistinguishedName $"CN=PC%d{i},OU=MZW1,OU=Unterricht,DC=htlvb,DC=intern")
            use! __ =
                computers
                |> List.map (fun computerDn -> async { return! createNodeAndParents ldap computerDn ADComputer [] })
                |> Async.Sequential
                |> Async.map (Seq.rev >> AsyncDisposable.combine)

            let! nodes = ldap.FindDescendantComputers(DistinguishedName "OU=Unterricht,DC=htlvb,DC=intern", [||])
            let nodeDns = nodes |> List.map (fun v -> DistinguishedName v.DistinguishedName)

            Expect.equal (Set.ofList nodeDns) (Set.ofList computers) "Should find all computers"
        })

        testCaseTask "Move node to new OU" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let sourceDn = DistinguishedName "CN=EINI1,OU=4AHWII,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap sourceDn ADUser []
            let targetDn = DistinguishedName "CN=EINI2,OU=5AHWII,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap (DN.parent targetDn) ADOrganizationalUnit []

            do! ldap.MoveNode(sourceDn, targetDn)
            use __ = async { do! ldap.DeleteNode(targetDn)  } |> Async.toAsyncDisposable
            let! oldNode = ldap.FindObjectByDn(sourceDn, [||]) |> Async.Catch
            let! newNode = ldap.FindObjectByDn(targetDn, [||]) |> Async.Catch

            Expect.isChoice2Of2 oldNode "Old node should be gone"
            Expect.isChoice1Of2 newNode "New node should be found"
        })

        testCaseTask "Move node to existing OU" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let sourceDn = DistinguishedName "CN=EINI1,OU=6AHWII,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap sourceDn ADUser []
            let targetDn = DistinguishedName "CN=EINI2,CN=Users,DC=htlvb,DC=intern"

            do! ldap.MoveNode(sourceDn, targetDn)
            use __ = async { do! ldap.DeleteNode(targetDn) } |> Async.toAsyncDisposable
            let! oldNode = ldap.FindObjectByDn(sourceDn, [||]) |> Async.Catch
            let! newNode = ldap.FindObjectByDn(targetDn, [||]) |> Async.Catch

            Expect.isChoice2Of2 oldNode "Old node should be gone"
            Expect.isChoice1Of2 newNode "New node should be found"
        })

        testCaseTask "Set node properties" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINJ,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap userDn ADUser [ "displayName", Text "Einstein Johann" ]
            do! ldap.SetNodeProperties(userDn, [ ("displayName", Text "Einstein Josef"); ("givenName", Text "Josef") ])
            let! node = ldap.FindObjectByDn(userDn, [| "displayName"; "givenName" |])

            let actualDisplayName = SearchResultEntry.getStringAttributeValue "displayName" node
            Expect.equal actualDisplayName "Einstein Josef" "Should have new display name"
            let actualGivenName = SearchResultEntry.getStringAttributeValue "givenName" node
            Expect.equal actualGivenName "Josef" "Should have new given name"
        })

        testCaseTask "Replace text in node properties" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINK,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap userDn ADUser [
                ("displayName", Text "Einstein Karl")
                ("givenName", Text "Karl")
                ("homeDirectory", Text @"C:\Users\Students\Einstein.Karl")
            ]
            do! ldap.ReplaceTextInNodePropertyValues(userDn, [
                {|
                    Name = "displayName"
                    Pattern = Regex(@"(?<= )Karl$")
                    Replacement = "Konrad"
                |}
                {|
                    Name = "homeDirectory"
                    Pattern = Regex(@"(?<=\.)Karl$")
                    Replacement = "Konrad"
                |}
            ])
            let! node = ldap.FindObjectByDn(userDn, [| "displayName"; "givenName"; "homeDirectory" |])

            let actualDisplayName = SearchResultEntry.getStringAttributeValue "displayName" node
            Expect.equal actualDisplayName "Einstein Konrad" "Should have new display name"
            let actualGivenName = SearchResultEntry.getStringAttributeValue "givenName" node
            Expect.equal actualGivenName "Karl" "Given name should not have changed"
            let actualHomePath = SearchResultEntry.getStringAttributeValue "homeDirectory" node
            Expect.equal actualHomePath @"C:\Users\Students\Einstein.Konrad" "Should have new home path"
        })

        testCaseTask "Disable account" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINL,CN=Users,DC=htlvb,DC=intern"
            use! __ = createUser ldap userDn []

            do! ldap.DisableAccount(userDn)

            Expect.throws (fun () ->
                let userName = let (DistinguishedName v) = userDn in v
                use c = new Ldap({ connectionConfig.Ldap with UserName = userName; Password = userPassword })
                c.FindObjectByDn(userDn, [||]) |> Async.Ignore |> Async.RunSynchronously
            ) "Login succeeded for disabled account"
        })

        testCaseTask "Add object to group" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINM9,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap userDn ADUser []
            use! temporaryGroup = createTemporaryGroup ldap "EINM"
            let! group = ldap.FindObjectByDn(temporaryGroup.Dn, [| "member" |])
            let membersBefore = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName

            do! ldap.AddObjectToGroup(temporaryGroup.Dn, userDn)
            let! group = ldap.FindObjectByDn(temporaryGroup.Dn, [| "member" |])
            let membersAfter = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName

            Expect.equal (membersAfter |> List.except membersBefore) [ userDn ] "New member should have been added"
        })

        testCaseTask "Add member-object to group" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            use! temporaryGroup = createTemporaryGroup ldap "EINN"
            let! group = ldap.FindObjectByDn(temporaryGroup.Dn, [| "member" |])
            let membersBefore = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName
            let addedMember = membersBefore |> List.head

            do! ldap.AddObjectToGroup(temporaryGroup.Dn, addedMember)
            let! group = ldap.FindObjectByDn(temporaryGroup.Dn, [| "member" |])
            let membersAfter = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName

            Expect.equal membersAfter membersBefore "Members should not have changed"
        })

        testCaseTask "Remove object from group" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            use! temporaryGroup = createTemporaryGroup ldap "EINO"
            let! group = ldap.FindObjectByDn(temporaryGroup.Dn, [| "member" |])
            let membersBefore = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName
            let removedMember = membersBefore |> List.head

            do! ldap.RemoveObjectFromGroup(temporaryGroup.Dn, removedMember)
            let! group = ldap.FindObjectByDn(temporaryGroup.Dn, [| "member" |])
            let membersAfter = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName

            Expect.equal (membersBefore |> List.except membersAfter) [ removedMember ] "First member should have been removed"
        })

        testCaseTask "Remove non-member-object from group" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINP9,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap userDn ADUser []
            use! temporaryGroup = createTemporaryGroup ldap "EINP"
            let! group = ldap.FindObjectByDn(temporaryGroup.Dn, [| "member" |])
            let membersBefore = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName

            do! ldap.RemoveObjectFromGroup(temporaryGroup.Dn, userDn)
            let! group = ldap.FindObjectByDn(temporaryGroup.Dn, [| "member" |])
            let membersAfter = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName

            Expect.equal (Set.ofList membersAfter) (Set.ofList membersBefore) "Members should not have changed"
        })

        testCaseTask "Remove multiple group memberships" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            use! temporaryGroup1 = createTemporaryGroup ldap "EINQ1"
            use! temporaryGroup2 = createTemporaryGroup ldap "EINQ2"
            use! temporaryGroup3 = createTemporaryGroup ldap "EINQ3"
            let! group1 = ldap.FindObjectByDn(temporaryGroup1.Dn, [| "member" |])
            let members1Before = SearchResultEntry.getStringAttributeValues "member" group1 |> List.map DistinguishedName
            let removedMember = members1Before |> List.head
            do! ldap.AddObjectToGroup(temporaryGroup2.Dn, removedMember)

            do! ldap.RemoveGroupMemberships(removedMember)

            let! group1 = ldap.FindObjectByDn(temporaryGroup1.Dn, [| "member" |])
            let members1After = SearchResultEntry.getStringAttributeValues "member" group1 |> List.map DistinguishedName
            let! group2 = ldap.FindObjectByDn(temporaryGroup2.Dn, [| "member" |])
            let members2After = SearchResultEntry.getStringAttributeValues "member" group2 |> List.map DistinguishedName
            let! group3 = ldap.FindObjectByDn(temporaryGroup3.Dn, [| "member" |])
            let members3After = SearchResultEntry.getStringAttributeValues "member" group3 |> List.map DistinguishedName

            Expect.isFalse (List.concat [ members1After; members2After; members3After ] |> List.contains removedMember) "User still has a group membership"
        })

        testCaseTask "Read string attribute value" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINZ,CN=Users,DC=htlvb,DC=intern"
            let displayName = "Cäsar Einstein"
            use! __ = createNodeAndParents ldap userDn ADUser [ "displayName", Text displayName ]
            let! node = ldap.FindObjectByDn(userDn, [| "displayName" |])

            let actualDisplayName = SearchResultEntry.getStringAttributeValue "displayName" node

            Expect.equal actualDisplayName displayName "User display name should be stored"
        })

        testCaseTask "Read string attribute values" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            use! temporaryGroup = createTemporaryGroup ldap "EINY"
            let! group = ldap.FindObjectByDn(temporaryGroup.Dn, [| "member" |])

            let actualMembers = SearchResultEntry.getStringAttributeValues "member" group

            Expect.isNonEmpty actualMembers "Member list should not be empty"
            Expect.all actualMembers (not << String.IsNullOrEmpty) "Group members should be stored"
        })

        testCaseTask "Read empty string attribute values" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINX,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap userDn ADUser []
            let! node = ldap.FindObjectByDn(userDn, [| "proxyAddresses" |])

            let actualProxyAddresses = SearchResultEntry.getStringAttributeValues "proxyAddresses" node

            Expect.isEmpty actualProxyAddresses "Proxy addresses should be empty"
        })

        testCaseTask "Read optional string attribute value" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINW,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap userDn ADUser []
            let! node = ldap.FindObjectByDn(userDn, [| "displayName" |])

            let actualDisplayName = SearchResultEntry.getOptionalStringAttributeValue "displayName" node

            Expect.isNone actualDisplayName "Display name should be None"
        })

        testCaseTask "Read timestamp attribute value" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINV,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap userDn ADUser []
            let! node = ldap.FindObjectByDn(userDn, [| "createTimeStamp" |])

            let actualCreationTime = SearchResultEntry.getDateTimeAttributeValue "createTimeStamp" node

            Expect.isLessThan (DateTime.Now - actualCreationTime) (TimeSpan.FromMinutes 1.) "User creation time should be stored"
        })

        testCaseTask "Read bytes attribute value" (fun () -> task {
            use ldap = new Ldap(connectionConfig.Ldap)
            let userDn = DistinguishedName "CN=EINU,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents ldap userDn ADUser []
            let! node = ldap.FindObjectByDn(userDn, [||])

            let actualSID = SearchResultEntry.getBytesAttributeValue "objectSid" node

            Expect.isNonEmpty actualSID "Object SID should be set"
        })
    ]
