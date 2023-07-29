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
    let private createUserHomePath config user = async {
        let! user = async {
            use connection = Ldap.connect config.Ldap
            return! Ldap.findObjectByDn connection user [| "objectSid"; "homeDirectory" |]
        }
        let userSID =
            SearchResultEntry.getBytesAttributeValue "objectSid" user
            |> AD.trySID
            |> Option.defaultWith (fun () -> failwith $"Invalid SID of user {user.DistinguishedName}")
        let homePath = SearchResultEntry.getStringAttributeValue "homeDirectory" user

        use _ = NetworkConnection.create config.NetworkShare homePath

        let dir = Directory.CreateDirectory(homePath)
        let acl = dir.GetAccessControl()
        acl.SetAccessRuleProtection(isProtected = true, preserveInheritance = false)
        acl.AddAccessRule(FileSystemAccessRule(administratorsSID, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(userSID, FileSystemRights.Modify, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        dir.SetAccessControl(acl)
    }
    let private moveUserHomePath config userDn newHomePath = async {
        use connection = Ldap.connect config.Ldap
        let! user = Ldap.findObjectByDn connection userDn [| "homeDirectory" |]
        let currentHomePath = SearchResultEntry.getStringAttributeValue "homeDirectory" user

        if currentHomePath <> newHomePath then
            use _ = NetworkConnection.create config.NetworkShare currentHomePath
            use _ = NetworkConnection.create config.NetworkShare newHomePath

            try
                Directory.Move(currentHomePath, newHomePath)
            with e ->
                failwith $"Failed to move user home path \"{currentHomePath}\" to \"{newHomePath}\": {e.Message}"

        do! Ldap.setNodeProperties connection userDn [ "homeDirectory", Text newHomePath ]
    }
    let private deleteUserHomePath config user = async {
        let! user = async {
            use connection = Ldap.connect config.Ldap
            return! Ldap.findObjectByDn connection user [| "homeDirectory" |]
        }
        let homePath = SearchResultEntry.getStringAttributeValue "homeDirectory" user
        try
            use _ = NetworkConnection.create config.NetworkShare homePath
            Directory.delete homePath
        with e ->
            failwith $"Failed to delete user home path \"{homePath}\": {e.Message}"
    }
    let private createExercisePath config teacher basePath (groups: {| Teachers: DistinguishedName; Students: DistinguishedName; TestUsers: DistinguishedName |}) = async {
        use connection = Ldap.connect config.Ldap

        let findSIDByDn objectDn = async {
            let! object = Ldap.findObjectByDn connection objectDn [| "objectSid" |]
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

        use _ = NetworkConnection.create config.NetworkShare basePath

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
    let rec run config operation = async {
        match operation with
        | CreateNode v ->
            use connection = Ldap.connect config.Ldap
            do! Ldap.createNodeAndParents connection v.Node v.NodeType v.Properties |> Async.Ignore
        | MoveNode v ->
            use connection = Ldap.connect config.Ldap
            do! Ldap.moveNode connection v.Source v.Target
        | SetNodeProperties v ->
            use connection = Ldap.connect config.Ldap
            do! Ldap.setNodeProperties connection v.Node v.Properties
        | ReplaceTextInNodePropertyValues v ->
            use connection = Ldap.connect config.Ldap
            do! Ldap.replaceTextInNodePropertyValues connection v.Node v.Properties
        | DeleteNode node ->
            use connection = Ldap.connect config.Ldap
            do! Ldap.deleteNode connection node
        | AddObjectToGroup v ->
            use connection = Ldap.connect config.Ldap
            do! Ldap.addObjectToGroup connection v.Group v.Object
        | RemoveObjectFromGroup v ->
            use connection = Ldap.connect config.Ldap
            do! Ldap.removeObjectFromGroup connection v.Group v.Object
        | CreateGroupHomePath path -> createGroupHomePath config.NetworkShare path
        | CreateUserHomePath user -> do! createUserHomePath config user
        | MoveUserHomePath v -> do! moveUserHomePath config v.User v.HomePath
        | DeleteUserHomePath user -> do! deleteUserHomePath config user
        | CreateExercisePath v -> do! createExercisePath config v.Teacher v.Path v.Groups
        | DeleteDirectory path -> deleteDirectory path
        | MoveDirectory v -> moveDirectory config.NetworkShare v.Source v.Target
        | ForEachGroupMember v ->
            let! memberDns = async {
                use connection = Ldap.connect config.Ldap
                let! group = Ldap.findObjectByDn connection v.Group [| "member" |]
                return
                    group
                    |> SearchResultEntry.getStringAttributeValues "member"
                    |> List.map DistinguishedName
            }
            do!
                memberDns
                |> List.map (fun memberDn ->
                    v.Operations memberDn
                    |> List.map (run config)
                    |> Async.Sequential
                    |> Async.Ignore
                )
                |> Async.Sequential
                |> Async.Ignore
    }
