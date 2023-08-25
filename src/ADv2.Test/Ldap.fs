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

let private createTemporaryGroup connection name = task {
    let members =
        [1..3]
        |> List.map (fun i -> DistinguishedName $"CN=%s{name}%d{i},CN=Users,DC=htlvb,DC=intern")
    let! createdMemberNodes =
        members
        |> List.map (fun memberDn -> createNodeAndParents connection memberDn ADUser [])
        |> Async.Sequential
        |> Async.map (Seq.rev >> AsyncDisposable.combine)
    let membersProperty =
        [ for DistinguishedName dn in members -> dn ]
        |> TextList
    let groupDn = DistinguishedName $"CN=%s{name},CN=Users,DC=htlvb,DC=intern"
    let! createdGroupNodes = createNodeAndParents connection groupDn ADGroup ["member", membersProperty ]
    return TemporaryGroup(groupDn, AsyncDisposable.combine [ createdGroupNodes; createdMemberNodes ])
}

let private userPassword = "Test123"
let private createUser connection userDn properties = async {
    return! createNodeAndParents connection userDn ADUser [
        yield! properties
        yield! DN.tryCN userDn |> Option.map (fun userName -> "sAMAccountName", Text userName) |> Option.toList
        ("userAccountControl", Text $"{0x10220}") // PASSWD_NOTREQD | NORMAL_ACCOUNT | DONT_EXPIRE_PASSWORD
        ("unicodePwd", Bytes (AD.password userPassword))
    ]
}

let tests =
    testList "Ldap" [
        testCaseTask "Create node" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let userDn = DistinguishedName "CN=EINA,CN=Users,DC=htlvb,DC=intern"

            use! __ = createNodeAndParents connection userDn ADUser []
            let! node = Ldap.findObjectByDn connection userDn [||]

            Expect.isNotNull node "Node should be found after creation"
        })

        testCaseTask "Find non-existing node" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let userDn = DistinguishedName "CN=EINB,CN=Users,DC=htlvb,DC=intern"

            let! findResult = Ldap.findObjectByDn connection userDn [||] |> Async.Catch

            Expect.isChoice2Of2 findResult "Node should not be found"
        })

        testCaseTask "Create node twice" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let userDn = DistinguishedName "CN=EINC,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents connection userDn ADUser []

            let! createdNodes = Ldap.createNodeAndParents connection userDn ADUser []

            Expect.isEmpty createdNodes "Creating node twice should be no-op"
        })

        testCaseTask "Create node and parent OUs" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap

            use! __ = createNodeAndParents connection (DistinguishedName "CN=EIND,OU=Lehrer,OU=Benutzer,OU=HTL,DC=htlvb,DC=intern") ADUser []
            let! node = Ldap.findObjectByDn connection (DistinguishedName "CN=EIND,OU=Lehrer,OU=Benutzer,OU=HTL,DC=htlvb,DC=intern") [||]

            Expect.isNotNull node "Node should be found after creation"
        })

        testCaseTask "Delete node" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let userDn = DistinguishedName "CN=EINE,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents connection userDn ADUser []

            do! Ldap.deleteNode connection userDn
            let! findResult = Ldap.findObjectByDn connection userDn [||] |> Async.Catch

            Expect.isChoice2Of2 findResult "Node should not be found after deletion"
        })

        testCaseTask "Find group members" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            use! group = createTemporaryGroup connection "EINF"

            let! actualMembers = Ldap.findGroupMembersIfGroupExists connection group.Dn

            Expect.isNonEmpty actualMembers "Member list should not be empty"
            Expect.all actualMembers (fun (DistinguishedName v) -> not <| String.IsNullOrEmpty v) "Group members should be stored"
        })

        testCaseTask "Find group members when group doesn't exist" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap

            let! actualMembers = Ldap.findGroupMembersIfGroupExists connection (DistinguishedName "CN=NoGroup,OU=HTLVB-Groups,DC=htlvb,DC=intern")

            Expect.isEmpty actualMembers "Member list is not empty"
        })

        testCaseTask "Find full group members" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            use! group = createTemporaryGroup connection "EING"

            let! members = Ldap.findFullGroupMembers connection group.Dn [| "objectSid" |]

            Expect.isNonEmpty members "Group should have members"
            Expect.all members (fun m -> not <| Array.isEmpty (m.Attributes.["objectSid"][0] :?> byte[])) "Group members should be stored"
        })

        testCaseTask "Find descendant users" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let users =
                [1..3]
                |> List.map (fun i -> DistinguishedName $"CN=EINH%d{i},OU=3AHWII,OU=Students,DC=htlvb,DC=intern")
            use! __ =
                users
                |> List.map (fun userDn -> async { return! createNodeAndParents connection userDn ADUser [] })
                |> Async.Sequential
                |> Async.map (Seq.rev >> AsyncDisposable.combine)

            let! nodes = Ldap.findDescendantUsers connection (DistinguishedName "OU=Students,DC=htlvb,DC=intern") [||]
            let nodeDns = nodes |> List.map (fun v -> DistinguishedName v.DistinguishedName)

            Expect.equal (Set.ofList nodeDns) (Set.ofList users) "Should find all users"
        })

        testCaseTask "Find descendant computers" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let computers =
                [1..3]
                |> List.map (fun i -> DistinguishedName $"CN=PC%d{i},OU=MZW1,OU=Unterricht,DC=htlvb,DC=intern")
            use! __ =
                computers
                |> List.map (fun computerDn -> async { return! createNodeAndParents connection computerDn ADComputer [] })
                |> Async.Sequential
                |> Async.map (Seq.rev >> AsyncDisposable.combine)

            let! nodes = Ldap.findDescendantComputers connection (DistinguishedName "OU=Unterricht,DC=htlvb,DC=intern") [||]
            let nodeDns = nodes |> List.map (fun v -> DistinguishedName v.DistinguishedName)

            Expect.equal (Set.ofList nodeDns) (Set.ofList computers) "Should find all computers"
        })

        testCaseTask "Move node to new OU" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let sourceDn = DistinguishedName "CN=EINI1,OU=4AHWII,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents connection sourceDn ADUser []
            let targetDn = DistinguishedName "CN=EINI2,OU=5AHWII,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents connection (DN.parent targetDn) ADOrganizationalUnit []

            do! Ldap.moveNode connection sourceDn targetDn
            use __ = async { do! Ldap.deleteNode connection targetDn  } |> Async.toAsyncDisposable
            let! oldNode = Ldap.findObjectByDn connection sourceDn [||] |> Async.Catch
            let! newNode = Ldap.findObjectByDn connection targetDn [||] |> Async.Catch

            Expect.isChoice2Of2 oldNode "Old node should be gone"
            Expect.isChoice1Of2 newNode "New node should be found"
        })

        testCaseTask "Move node to existing OU" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let sourceDn = DistinguishedName "CN=EINI1,OU=6AHWII,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents connection sourceDn ADUser []
            let targetDn = DistinguishedName "CN=EINI2,CN=Users,DC=htlvb,DC=intern"

            do! Ldap.moveNode connection sourceDn targetDn
            use __ = async { do! Ldap.deleteNode connection targetDn } |> Async.toAsyncDisposable
            let! oldNode = Ldap.findObjectByDn connection sourceDn [||] |> Async.Catch
            let! newNode = Ldap.findObjectByDn connection targetDn [||] |> Async.Catch

            Expect.isChoice2Of2 oldNode "Old node should be gone"
            Expect.isChoice1Of2 newNode "New node should be found"
        })

        testCaseTask "Set node properties" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let userDn = DistinguishedName "CN=EINJ,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents connection userDn ADUser [ "displayName", Text "Einstein Johann" ]
            do! Ldap.setNodeProperties connection userDn [ ("displayName", Text "Einstein Josef"); ("givenName", Text "Josef") ]
            let! node = Ldap.findObjectByDn connection userDn [| "displayName"; "givenName" |]

            let actualDisplayName = SearchResultEntry.getStringAttributeValue "displayName" node
            Expect.equal actualDisplayName "Einstein Josef" "Should have new display name"
            let actualGivenName = SearchResultEntry.getStringAttributeValue "givenName" node
            Expect.equal actualGivenName "Josef" "Should have new given name"
        })

        testCaseTask "Replace text in node properties" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let userDn = DistinguishedName "CN=EINK,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents connection userDn ADUser [
                ("displayName", Text "Einstein Karl")
                ("givenName", Text "Karl")
                ("homeDirectory", Text @"C:\Users\Students\Einstein.Karl")
            ]
            do! Ldap.replaceTextInNodePropertyValues connection userDn [
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
            ]
            let! node = Ldap.findObjectByDn connection userDn [| "displayName"; "givenName"; "homeDirectory" |]

            let actualDisplayName = SearchResultEntry.getStringAttributeValue "displayName" node
            Expect.equal actualDisplayName "Einstein Konrad" "Should have new display name"
            let actualGivenName = SearchResultEntry.getStringAttributeValue "givenName" node
            Expect.equal actualGivenName "Karl" "Given name should not have changed"
            let actualHomePath = SearchResultEntry.getStringAttributeValue "homeDirectory" node
            Expect.equal actualHomePath @"C:\Users\Students\Einstein.Konrad" "Should have new home path"
        })

        testCaseTask "Disable account" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let userDn = DistinguishedName "CN=EINL,CN=Users,DC=htlvb,DC=intern"
            use! __ = createUser connection userDn []

            do! Ldap.disableAccount connection userDn

            Expect.throws (fun () ->
                let userName = let (DistinguishedName v) = userDn in v
                use c = Ldap.connect { connectionConfig.Ldap with UserName = userName; Password = userPassword }
                c.Bind()
            ) "Login succeeded for disabled account"
        })

        testCaseTask "Add object to group" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let userDn = DistinguishedName "CN=EINM9,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents connection userDn ADUser []
            use! temporaryGroup = createTemporaryGroup connection "EINM"
            let! group = Ldap.findObjectByDn connection temporaryGroup.Dn [| "member" |]
            let membersBefore = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName

            do! Ldap.addObjectToGroup connection temporaryGroup.Dn userDn
            let! group = Ldap.findObjectByDn connection temporaryGroup.Dn [| "member" |]
            let membersAfter = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName

            Expect.equal (membersAfter |> List.except membersBefore) [ userDn ] "New member should have been added"
        })

        testCaseTask "Add member-object to group" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            use! temporaryGroup = createTemporaryGroup connection "EINN"
            let! group = Ldap.findObjectByDn connection temporaryGroup.Dn [| "member" |]
            let membersBefore = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName
            let addedMember = membersBefore |> List.head

            do! Ldap.addObjectToGroup connection temporaryGroup.Dn addedMember
            let! group = Ldap.findObjectByDn connection temporaryGroup.Dn [| "member" |]
            let membersAfter = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName

            Expect.equal membersAfter membersBefore "Members should not have changed"
        })

        testCaseTask "Remove object from group" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            use! temporaryGroup = createTemporaryGroup connection "EINO"
            let! group = Ldap.findObjectByDn connection temporaryGroup.Dn [| "member" |]
            let membersBefore = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName
            let removedMember = membersBefore |> List.head

            do! Ldap.removeObjectFromGroup connection temporaryGroup.Dn removedMember
            let! group = Ldap.findObjectByDn connection temporaryGroup.Dn [| "member" |]
            let membersAfter = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName

            Expect.equal (membersBefore |> List.except membersAfter) [ removedMember ] "First member should have been removed"
        })

        testCaseTask "Remove non-member-object from group" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let userDn = DistinguishedName "CN=EINP9,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents connection userDn ADUser []
            use! temporaryGroup = createTemporaryGroup connection "EINP"
            let! group = Ldap.findObjectByDn connection temporaryGroup.Dn [| "member" |]
            let membersBefore = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName

            do! Ldap.removeObjectFromGroup connection temporaryGroup.Dn userDn
            let! group = Ldap.findObjectByDn connection temporaryGroup.Dn [| "member" |]
            let membersAfter = SearchResultEntry.getStringAttributeValues "member" group |> List.map DistinguishedName

            Expect.equal (Set.ofList membersAfter) (Set.ofList membersBefore) "Members should not have changed"
        })

        testCaseTask "Remove multiple group memberships" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            use! temporaryGroup1 = createTemporaryGroup connection "EINQ1"
            use! temporaryGroup2 = createTemporaryGroup connection "EINQ2"
            use! temporaryGroup3 = createTemporaryGroup connection "EINQ3"
            let! group1 = Ldap.findObjectByDn connection temporaryGroup1.Dn [| "member" |]
            let members1Before = SearchResultEntry.getStringAttributeValues "member" group1 |> List.map DistinguishedName
            let removedMember = members1Before |> List.head
            do! Ldap.addObjectToGroup connection temporaryGroup2.Dn removedMember

            do! Ldap.removeGroupMemberships connection removedMember

            let! group1 = Ldap.findObjectByDn connection temporaryGroup1.Dn [| "member" |]
            let members1After = SearchResultEntry.getStringAttributeValues "member" group1 |> List.map DistinguishedName
            let! group2 = Ldap.findObjectByDn connection temporaryGroup2.Dn [| "member" |]
            let members2After = SearchResultEntry.getStringAttributeValues "member" group2 |> List.map DistinguishedName
            let! group3 = Ldap.findObjectByDn connection temporaryGroup3.Dn [| "member" |]
            let members3After = SearchResultEntry.getStringAttributeValues "member" group3 |> List.map DistinguishedName

            Expect.isFalse (List.concat [ members1After; members2After; members3After ] |> List.contains removedMember) "User still has a group membership"
        })

        testCaseTask "Read string attribute value" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let userDn = DistinguishedName "CN=EINZ,CN=Users,DC=htlvb,DC=intern"
            let displayName = "Cäsar Einstein"
            use! __ = createNodeAndParents connection userDn ADUser [ "displayName", Text displayName ]
            let! node = Ldap.findObjectByDn connection userDn [| "displayName" |]

            let actualDisplayName = SearchResultEntry.getStringAttributeValue "displayName" node

            Expect.equal actualDisplayName displayName "User display name should be stored"
        })

        testCaseTask "Read string attribute values" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            use! temporaryGroup = createTemporaryGroup connection "EINY"
            let! group = Ldap.findObjectByDn connection temporaryGroup.Dn [| "member" |]

            let actualMembers = SearchResultEntry.getStringAttributeValues "member" group

            Expect.isNonEmpty actualMembers "Member list should not be empty"
            Expect.all actualMembers (not << String.IsNullOrEmpty) "Group members should be stored"
        })

        testCaseTask "Read empty string attribute values" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let userDn = DistinguishedName "CN=EINX,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents connection userDn ADUser []
            let! node = Ldap.findObjectByDn connection userDn [| "proxyAddresses" |]

            let actualProxyAddresses = SearchResultEntry.getStringAttributeValues "proxyAddresses" node

            Expect.isEmpty actualProxyAddresses "Proxy addresses should be empty"
        })

        testCaseTask "Read optional string attribute value" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let userDn = DistinguishedName "CN=EINW,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents connection userDn ADUser []
            let! node = Ldap.findObjectByDn connection userDn [| "displayName" |]

            let actualDisplayName = SearchResultEntry.getOptionalStringAttributeValue "displayName" node

            Expect.isNone actualDisplayName "Display name should be None"
        })

        testCaseTask "Read timestamp attribute value" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let userDn = DistinguishedName "CN=EINV,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents connection userDn ADUser []
            let! node = Ldap.findObjectByDn connection userDn [| "createTimeStamp" |]

            let actualCreationTime = SearchResultEntry.getDateTimeAttributeValue "createTimeStamp" node

            Expect.isLessThan (DateTime.Now - actualCreationTime) (TimeSpan.FromMinutes 1.) "User creation time should be stored"
        })

        testCaseTask "Read bytes attribute value" (fun () -> task {
            use connection = Ldap.connect connectionConfig.Ldap
            let userDn = DistinguishedName "CN=EINU,CN=Users,DC=htlvb,DC=intern"
            use! __ = createNodeAndParents connection userDn ADUser []
            let! node = Ldap.findObjectByDn connection userDn [||]

            let actualSID = SearchResultEntry.getBytesAttributeValue "objectSid" node

            Expect.isNonEmpty actualSID "Object SID should be set"
        })
    ]
