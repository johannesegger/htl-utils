module AD.Operations

open AD.Configuration
open AD.Directory
open AD.Ldap
open System.IO
open System.Security.AccessControl
open System.Security.Principal
open System.Text.RegularExpressions

type Operation =
    | CreateNode of
        {|
            Node: DistinguishedName
            NodeType: NodeType
            Properties: (string * PropertyValue) list
        |}
    | MoveNode of
        {|
            Source: DistinguishedName
            Target: DistinguishedName
        |}
    | DeleteNode of DistinguishedName
    | SetNodeProperties of
        {|
            Node: DistinguishedName
            Properties: (string * PropertyValue) list
        |}
    | ReplaceTextInNodePropertyValues of
        {|
            Node: DistinguishedName
            Properties: {| Name: string; Pattern: Regex; Replacement: string |} list
        |}
    | EnableAccount of DistinguishedName
    | DisableAccount of DistinguishedName
    | RemoveGroupMemberships of DistinguishedName
    | AddObjectToGroup of
        {|
            Object: DistinguishedName
            Group: DistinguishedName
        |}
    | RemoveObjectFromGroup of
        {|
            Object: DistinguishedName
            Group: DistinguishedName
        |}
    | CreateGroupHomePath of string
    | CreateUserHomePath of DistinguishedName
    | MoveUserHomePath of
        {|
            User: DistinguishedName
            HomePath: string
        |}
    | DeleteUserHomePath of DistinguishedName
    | CreateExercisePath of
        {|
            Teacher: DistinguishedName
            Path: string
            Groups: {|
                Teachers: DistinguishedName
                Students: DistinguishedName
                TestUsers: DistinguishedName
            |}
        |}
    | MoveDirectory of
        {|
            Source: string
            Target: string
        |}
    | DeleteDirectory of string
    | ForEachGroupMember of
        {|
            Group: DistinguishedName
            Operations: DistinguishedName -> Operation list
        |}

module Operation =
    let private administratorsSID =
        SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)

    let private localSystemSID =
        SecurityIdentifier(WellKnownSidType.LocalSystemSid, null)

    let private creatorOwnerSID =
        SecurityIdentifier(WellKnownSidType.CreatorOwnerSid, null)

    let private createGroupHomePath config path =
        use _ = NetworkConnection.create config path
        Directory.CreateDirectory(path) |> ignore
    let private createUserHomePath (ldap: Ldap) networkShareConnectionConfig user = async {
        let! user = async {
            return! ldap.FindObjectByDn(user, [| "objectSid"; "homeDirectory" |])
        }
        let userSID =
            SearchResultEntry.getBytesAttributeValue "objectSid" user
            |> AD.trySID
            |> Option.defaultWith (fun () -> failwith $"Invalid SID of user {user.DistinguishedName}")
        let homePath = SearchResultEntry.getStringAttributeValue "homeDirectory" user

        use _ = NetworkConnection.create networkShareConnectionConfig homePath

        let dir = Directory.CreateDirectory(homePath)
        let acl = dir.GetAccessControl()
        acl.SetAccessRuleProtection(isProtected = true, preserveInheritance = false)
        acl.AddAccessRule(FileSystemAccessRule(administratorsSID, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(userSID, FileSystemRights.Modify, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        dir.SetAccessControl(acl)
    }
    let private moveUserHomePath (ldap: Ldap) networkShareConnectionConfig userDn newHomePath = async {
        let! user = ldap.FindObjectByDn(userDn, [| "homeDirectory" |])
        let currentHomePath = SearchResultEntry.getStringAttributeValue "homeDirectory" user

        if currentHomePath <> newHomePath then
            use _ = NetworkConnection.create networkShareConnectionConfig currentHomePath
            use _ = NetworkConnection.create networkShareConnectionConfig newHomePath

            try
                Directory.Move(currentHomePath, newHomePath)
            with e ->
                failwith $"Failed to move user home path \"{currentHomePath}\" to \"{newHomePath}\": {e.Message}"

        do! ldap.SetNodeProperties(userDn, [ "homeDirectory", Text newHomePath ])
    }
    let private deleteUserHomePath (ldap: Ldap) networkShareConnectionConfig user = async {
        let! user = ldap.FindObjectByDn(user, [| "homeDirectory" |])

        let homePath = SearchResultEntry.getStringAttributeValue "homeDirectory" user
        try
            use _ = NetworkConnection.create networkShareConnectionConfig homePath
            Directory.delete homePath
        with e ->
            failwith $"Failed to delete user home path \"{homePath}\": {e.Message}"
    }
    let private createExercisePath (ldap: Ldap) networkShareConnectionConfig teacher basePath (groups: {| Teachers: DistinguishedName; Students: DistinguishedName; TestUsers: DistinguishedName |}) = async {
        let findSIDByDn objectDn = async {
            let! object = ldap.FindObjectByDn(objectDn, [| "objectSid" |])
            return
                object
                |> SearchResultEntry.getBytesAttributeValue "objectSid"
                |> AD.trySID
                |> Option.defaultWith (fun () -> failwith $"Invalid SID of object {object.DistinguishedName}")
        }

        let! userSID = findSIDByDn teacher
        let! teacherGroupSID = findSIDByDn groups.Teachers
        let! studentGroupSID = findSIDByDn groups.Students
        let! testUserGroupSID = findSIDByDn groups.TestUsers

        use _ = NetworkConnection.create networkShareConnectionConfig basePath

        let dir = Directory.CreateDirectory(basePath)
        let acl = dir.GetAccessControl()
        acl.SetAccessRuleProtection(isProtected = true, preserveInheritance = false)
        acl.AddAccessRule(FileSystemAccessRule(localSystemSID, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(administratorsSID, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(teacherGroupSID, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(studentGroupSID, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(testUserGroupSID, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        dir.SetAccessControl(acl)

        let instructionDir = dir.CreateSubdirectory("Abgabe")
        let acl = instructionDir.GetAccessControl()
        acl.SetAccessRuleProtection(isProtected = true, preserveInheritance = false)
        acl.AddAccessRule(FileSystemAccessRule(localSystemSID, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(administratorsSID, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(creatorOwnerSID, FileSystemRights.CreateFiles ||| FileSystemRights.ReadAndExecute, InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(studentGroupSID, FileSystemRights.CreateFiles ||| FileSystemRights.AppendData ||| FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(userSID, FileSystemRights.Modify, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(userSID, FileSystemRights.CreateFiles ||| FileSystemRights.AppendData ||| FileSystemRights.ReadAndExecute, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow))
        instructionDir.SetAccessControl(acl)

        let testInstructionDir = dir.CreateSubdirectory("Abgabe_SA")
        let acl = testInstructionDir.GetAccessControl()
        acl.SetAccessRuleProtection(isProtected = true, preserveInheritance = false)
        acl.AddAccessRule(FileSystemAccessRule(localSystemSID, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(administratorsSID, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(creatorOwnerSID, FileSystemRights.CreateFiles ||| FileSystemRights.ReadAndExecute, InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(testUserGroupSID, FileSystemRights.CreateFiles ||| FileSystemRights.AppendData ||| FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(userSID, FileSystemRights.Modify, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(userSID, FileSystemRights.CreateFiles ||| FileSystemRights.AppendData ||| FileSystemRights.ReadAndExecute, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow))
        testInstructionDir.SetAccessControl(acl)

        let deliveryDir = dir.CreateSubdirectory("Angabe")
        let acl = deliveryDir.GetAccessControl()
        acl.SetAccessRuleProtection(isProtected = true, preserveInheritance = false)
        acl.AddAccessRule(FileSystemAccessRule(localSystemSID, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(administratorsSID, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(teacherGroupSID, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(studentGroupSID, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(userSID, FileSystemRights.Modify, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(userSID, FileSystemRights.ReadData ||| FileSystemRights.CreateFiles ||| FileSystemRights.AppendData, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow))
        deliveryDir.SetAccessControl(acl)

        let testDeliveryDir = dir.CreateSubdirectory("Angabe_SA")
        let acl = testDeliveryDir.GetAccessControl()
        acl.SetAccessRuleProtection(isProtected = true, preserveInheritance = false)
        acl.AddAccessRule(FileSystemAccessRule(localSystemSID, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(administratorsSID, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(teacherGroupSID, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(userSID, FileSystemRights.Modify, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(userSID, FileSystemRights.ReadData ||| FileSystemRights.CreateFiles ||| FileSystemRights.AppendData, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(testUserGroupSID, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        testDeliveryDir.SetAccessControl(acl)
    }
    let private deleteDirectory path =
        try
            Directory.delete path
        with e ->
            failwith $"Failed to delete directory \"{path}\": {e.Message}"
    let private moveDirectory config source target =
        use _ = NetworkConnection.create config source
        use _ = NetworkConnection.create config target

        try
            Directory.Move(source, target)
        with e ->
            failwith $"Failed to move directory \"{source}\" to \"{target}\": {e.Message}"
    let rec run (ldap: Ldap) networkShareConnectionConfig operation = async {
        match operation with
        | CreateNode v ->
            do! ldap.CreateNodeAndParents(v.Node, v.NodeType, v.Properties) |> Async.Ignore
        | MoveNode v ->
            do! ldap.MoveNode(v.Source, v.Target)
        | SetNodeProperties v ->
            do! ldap.SetNodeProperties(v.Node, v.Properties)
        | ReplaceTextInNodePropertyValues v ->
            do! ldap.ReplaceTextInNodePropertyValues(v.Node, v.Properties)
        | DisableAccount userDn ->
            do! ldap.DisableAccount(userDn)
        | EnableAccount userDn ->
            do! ldap.EnableAccount(userDn)
        | RemoveGroupMemberships userDn ->
            do! ldap.RemoveGroupMemberships(userDn)
        | DeleteNode node ->
            do! ldap.DeleteNode(node)
        | AddObjectToGroup v ->
            do! ldap.AddObjectToGroup(v.Group, v.Object)
        | RemoveObjectFromGroup v ->
            do! ldap.RemoveObjectFromGroup(v.Group, v.Object)
        | CreateGroupHomePath path -> createGroupHomePath networkShareConnectionConfig path
        | CreateUserHomePath user -> do! createUserHomePath ldap networkShareConnectionConfig user
        | MoveUserHomePath v -> do! moveUserHomePath ldap networkShareConnectionConfig v.User v.HomePath
        | DeleteUserHomePath user -> do! deleteUserHomePath ldap networkShareConnectionConfig user
        | CreateExercisePath v -> do! createExercisePath ldap networkShareConnectionConfig v.Teacher v.Path v.Groups
        | DeleteDirectory path -> deleteDirectory path
        | MoveDirectory v -> moveDirectory networkShareConnectionConfig v.Source v.Target
        | ForEachGroupMember v ->
            let! memberDns = async {
                let! group = ldap.FindObjectByDn(v.Group, [| "member" |])
                return
                    group
                    |> SearchResultEntry.getStringAttributeValues "member"
                    |> List.map DistinguishedName
            }
            do!
                memberDns
                |> List.map (fun memberDn ->
                    v.Operations memberDn
                    |> List.map (run ldap networkShareConnectionConfig)
                    |> Async.Sequential
                    |> Async.Ignore
                )
                |> Async.Sequential
                |> Async.Ignore
    }
