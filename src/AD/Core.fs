module AD.Core

open AD.Configuration
open AD.Domain
open CPI.DirectoryServices
open System
open System.DirectoryServices
open System.IO
open System.Security.AccessControl
open System.Security.Principal

// see http://www.gabescode.com/active-directory/2018/12/15/better-performance-activedirectory.html

module private DN =
    let private child name (DistinguishedName path) =
        let dn = DN(path)
        dn.GetChild(name).ToString() |> DistinguishedName

    let childOU name = child (sprintf "OU=%s" name)
    let childCN name = child (sprintf "CN=%s" name)

    let parent (DistinguishedName path) =
        DistinguishedName (DN(path).Parent.ToString())

    let head (DistinguishedName path) =
        DN(path).RDNs
        |> Seq.tryHead
        |> Option.bind (fun v -> v.Components |> Seq.tryExactlyOne)
        |> Option.map (fun v -> v.ComponentType, v.ComponentValue)
        |> Option.defaultWith (fun () -> failwithf "Can't get head from distinguished name \"\"")

    let parentsAndSelf (DistinguishedName path) =
        let rec fn (dn: DN) acc =
            let acc' = DistinguishedName (dn.ToString()) :: acc
            if Seq.isEmpty dn.RDNs
            then List.rev acc'
            else fn dn.Parent acc'

        fn (DN(path)) []

    let tryFindParent path filter =
        parentsAndSelf path
        |> Seq.tryFind (fun (DistinguishedName parentPath) -> DN(parentPath).RDNs |> Seq.head |> (fun v -> filter (v.ToString())))

    let isOU (DistinguishedName path) =
        let dn = DN(path)
        dn.RDNs
        |> Seq.tryHead
        |> Option.bind (fun v -> v.Components |> Seq.tryExactlyOne)
        |> Option.map (fun v -> CIString v.ComponentType = CIString "OU")
        |> Option.defaultValue false

    let tryCN (DistinguishedName path) =
        DN(path).RDNs
        |> Seq.tryItem 0
        |> Option.bind (fun v -> v.Components |> Seq.tryExactlyOne)
        |> Option.bind (fun v -> if CIString v.ComponentType = CIString "CN" then Some v.ComponentValue else None)

let private createNetworkConnection path = reader {
    let! config = Reader.environment
    return NetworkConnection.create config.UserName config.Password path
}

let internal adDirectoryEntry properties (DistinguishedName path) = reader {
    let! config = Reader.environment
    let entry = new DirectoryEntry(sprintf "LDAP://%s/%s" config.DomainControllerHostName path, config.UserName, config.Password)
    entry.RefreshCache(properties)
    return entry
}

let private groupHomePath userType = reader {
    let! config = Reader.environment
    match userType with
    | Teacher -> return config.TeacherHomePath
    | Student (GroupName className) -> return Path.Combine(config.StudentHomePath, className)
}

let private homePath (UserName userName) userType = reader {
    let! groupHomePath = groupHomePath userType
    return Path.Combine(groupHomePath, userName)
}

let private proxyAddresses firstName lastName mailDomain =
    [
        sprintf "smtp:%s.%s@%s" firstName lastName mailDomain
    ]

let private teacherExercisePath (UserName userName) = reader {
    let! config = Reader.environment
    return Path.Combine(config.TeacherExercisePath, userName)
}

let private createOU path =
    path
    |> DN.parentsAndSelf
    |> Seq.rev
    |> Seq.filter DN.isOU
    |> Seq.map (fun path -> reader {
        try
            let! adCtx = adDirectoryEntry [||] path
            adCtx.Guid |> ignore // Test if OU exists
            return adCtx
        with _ ->
            let parentPath = DN.parent path
            use! parentCtx = adDirectoryEntry [||] parentPath
            let child = parentCtx.Children.Add(uncurry (sprintf "%s=%s") (DN.head path), "organizationalUnit")
            child.CommitChanges()
            return child
    })
    |> Seq.tryLast

let private createCN path entryType = reader {
    let name = DN.tryCN path |> Option.defaultWith (fun () -> failwithf "Can't create CN entry: %A doesn't identify a CN entry" path)
    use! parentCtx = createOU (DN.parent path) |> Option.defaultWith (fun () -> failwithf "Error while creating %O" path)
    let child = parentCtx.Children.Add(sprintf "CN=%s" name, entryType)
    parentCtx.CommitChanges()
    return child
}

let private createGroupEntry path = createCN path "group"

let private userContainer userType = reader {
    let! config = Reader.environment
    match userType with
    | Teacher -> return config.TeacherContainer
    | Student (GroupName className) -> return DN.childOU className config.ClassContainer
}

let internal userRootEntry = userContainer >> Reader.bind (adDirectoryEntry [||])

let internal user ctx (UserName userName) properties =
    use searcher = new DirectorySearcher(ctx, sprintf "(&(objectCategory=person)(objectClass=user)(sAMAccountName=%s))" userName, properties)
    searcher.FindOne()

let private teacherGroupName = reader {
    let! config = Reader.environment
    return
        DN.tryCN config.TeacherGroup
        |> Option.defaultWith (fun () -> failwith "Can't get teacher group name: AD_TEACHER_GROUP must be a distinguished name with CN at the beginning")
        |> GroupName
}

let private studentGroupName = reader {
    let! config = Reader.environment
    return
        DN.tryCN config.StudentGroup
        |> Option.defaultWith (fun () -> failwith "Can't get student group name: AD_STUDENT_GROUP must be a distinguished name with CN at the beginning")
        |> GroupName
}

let internal groupPathFromUserType userType = reader {
    let! config = Reader.environment
    match userType with
    | Teacher -> return config.TeacherGroup
    | Student (GroupName className) -> return DN.childCN className config.ClassGroupsContainer
}

let private departmentFromUserType = function
    | Teacher -> teacherGroupName |> Reader.map (fun (GroupName name) -> name)
    | Student (GroupName className) -> Reader.retn className

let private divisionFromUserType = function
    | Teacher -> teacherGroupName |> Reader.map (fun (GroupName name) -> name)
    | Student _ -> studentGroupName |> Reader.map (fun (GroupName name) -> name)

let private sidFromDirectoryEntry (directoryEntry: DirectoryEntry) =
    directoryEntry.RefreshCache([| "objectSid" |])
    let data = directoryEntry.Properties.["objectSid"].[0] :?> byte array
    SecurityIdentifier(data, 0)

let private sidFromDistinguishedName path = reader {
    use! adCtx = adDirectoryEntry [||] path
    return sidFromDirectoryEntry adCtx
}

let private updateDirectoryEntry path properties fn = reader {
    use! adEntry = adDirectoryEntry properties path
    do! fn adEntry
    adEntry.CommitChanges()
}

let private updateGroup userType properties fn = reader {
    let! path = groupPathFromUserType userType
    do! updateDirectoryEntry path properties fn
}

let private updateUser userName userType properties fn = reader {
    use! adCtx = userRootEntry userType
    let searchResult = user adCtx userName properties
    use adUser = searchResult.GetDirectoryEntry()
    adUser.RefreshCache(properties)
    do! fn adUser
    adUser.CommitChanges()
}

let private createUser (newUser: NewUser) password = reader {
    use! adCtx = userRootEntry newUser.Type
    let (UserName userName) = newUser.Name
    let adUser = adCtx.Children.Add(sprintf "CN=%s" userName, "user")
    adCtx.CommitChanges()

    let! config = Reader.environment
    let userPrincipalName = sprintf "%s@%s" userName config.MailDomain
    adUser.Properties.["userPrincipalName"].Value <- userPrincipalName
    newUser.SokratesId |> Option.iter (fun (SokratesId v) -> adUser.Properties.[config.SokratesIdAttributeName].Value <- v)
    adUser.Properties.["givenName"].Value <- newUser.FirstName
    adUser.Properties.["sn"].Value <- newUser.LastName
    adUser.Properties.["displayName"].Value <- sprintf "%s %s" newUser.LastName newUser.FirstName
    adUser.Properties.["sAMAccountName"].Value <- userName
    adUser.Properties.["department"].Value <- departmentFromUserType newUser.Type
    adUser.Properties.["division"].Value <- divisionFromUserType newUser.Type
    adUser.Properties.["mail"].Value <- sprintf "%s.%s@%s" newUser.LastName newUser.FirstName config.MailDomain
    adUser.Properties.["proxyAddresses"].Value <- proxyAddresses newUser.FirstName newUser.LastName config.MailDomain |> List.toArray
    let! userHomePath = homePath newUser.Name newUser.Type
    adUser.Properties.["homeDirectory"].Value <- userHomePath
    adUser.Properties.["homeDrive"].Value <- config.HomeDrive
    adUser.Properties.["userAccountControl"].Value <- 0x220 // PASSWD_NOTREQD | NORMAL_ACCOUNT
    adUser.CommitChanges() // Must create user before setting password
    adUser.Invoke("SetPassword", [| password |]) |> ignore
    adUser.Properties.["pwdLastSet"].Value <- 0 // Expire password
    adUser.CommitChanges()

    do! updateGroup newUser.Type [| "member" |] (fun group -> reader {
        group.Properties.["member"].Add(adUser.Properties.["distinguishedName"].Value) |> ignore
    })

    use! __ = createNetworkConnection userHomePath

    let adUserSid = sidFromDirectoryEntry adUser

    do
        let dir = Directory.CreateDirectory(userHomePath)
        let acl = dir.GetAccessControl()
        acl.SetAccessRuleProtection(true, false) // Disable inheritance
        acl.AddAccessRule(FileSystemAccessRule(SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(adUserSid, FileSystemRights.Modify, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        dir.SetAccessControl(acl)

    match newUser.Type with
    | Teacher ->
        let! exercisePath = teacherExercisePath newUser.Name

        let! teacherSid = sidFromDistinguishedName config.TeacherGroup
        let! studentSid = sidFromDistinguishedName config.StudentGroup
        let! testUserSid = sidFromDistinguishedName config.TestUserGroup

        let dir = Directory.CreateDirectory(exercisePath)
        let acl = dir.GetAccessControl()
        acl.SetAccessRuleProtection(true, false) // Disable inheritance
        acl.AddAccessRule(FileSystemAccessRule(SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(teacherSid, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(studentSid, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(testUserSid, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        dir.SetAccessControl(acl)

        let instructionDir = dir.CreateSubdirectory("Abgabe")
        let acl = instructionDir.GetAccessControl()
        acl.SetAccessRuleProtection(true, false) // Disable inheritance
        acl.AddAccessRule(FileSystemAccessRule(SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(SecurityIdentifier(WellKnownSidType.CreatorOwnerSid, null), FileSystemRights.CreateFiles ||| FileSystemRights.ReadAndExecute, InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(studentSid, FileSystemRights.CreateFiles ||| FileSystemRights.AppendData ||| FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(adUserSid, FileSystemRights.Modify, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(adUserSid, FileSystemRights.CreateFiles ||| FileSystemRights.AppendData ||| FileSystemRights.ReadAndExecute, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow))
        instructionDir.SetAccessControl(acl)

        let testInstructionDir = dir.CreateSubdirectory("Abgabe_SA")
        let acl = testInstructionDir.GetAccessControl()
        acl.SetAccessRuleProtection(true, false) // Disable inheritance
        acl.AddAccessRule(FileSystemAccessRule(SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(SecurityIdentifier(WellKnownSidType.CreatorOwnerSid, null), FileSystemRights.CreateFiles ||| FileSystemRights.ReadAndExecute, InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(testUserSid, FileSystemRights.CreateFiles ||| FileSystemRights.AppendData ||| FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(adUserSid, FileSystemRights.Modify, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(adUserSid, FileSystemRights.CreateFiles ||| FileSystemRights.AppendData ||| FileSystemRights.ReadAndExecute, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow))
        testInstructionDir.SetAccessControl(acl)

        let deliveryDir = dir.CreateSubdirectory("Angabe")
        let acl = deliveryDir.GetAccessControl()
        acl.SetAccessRuleProtection(true, false) // Disable inheritance
        acl.AddAccessRule(FileSystemAccessRule(SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(teacherSid, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(studentSid, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(adUserSid, FileSystemRights.Modify, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(adUserSid, FileSystemRights.ReadData ||| FileSystemRights.CreateFiles ||| FileSystemRights.AppendData, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow))
        deliveryDir.SetAccessControl(acl)

        let testDeliveryDir = dir.CreateSubdirectory("Angabe_SA")
        let acl = testDeliveryDir.GetAccessControl()
        acl.SetAccessRuleProtection(true, false) // Disable inheritance
        acl.AddAccessRule(FileSystemAccessRule(SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(teacherSid, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(adUserSid, FileSystemRights.Modify, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(adUserSid, FileSystemRights.ReadData ||| FileSystemRights.CreateFiles ||| FileSystemRights.AppendData, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow))
        acl.AddAccessRule(FileSystemAccessRule(testUserSid, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
        testDeliveryDir.SetAccessControl(acl)
    | Student _ -> ()
}

let private changeUserName userName userType (UserName newUserName, newFirstName, newLastName) =
    updateUser userName userType [| "mail"; "homeDirectory"; "proxyAddresses" |] (fun adUser -> reader {
        let oldEMailAddress = adUser.Properties.["mail"].Value :?> string
        let oldHomeDirectory = adUser.Properties.["homeDirectory"].Value :?> string
        let oldProxyAddresses = adUser.Properties.["proxyAddresses"] |> Seq.cast<string> |> Seq.toList

        adUser.Rename(sprintf "CN=%s" newUserName)
        let! config = Reader.environment
        adUser.Properties.["userPrincipalName"].Value <- sprintf "%s@%s" newUserName config.MailDomain
        adUser.Properties.["givenName"].Value <- newFirstName
        adUser.Properties.["sn"].Value <- newLastName
        adUser.Properties.["displayName"].Value <- sprintf "%s %s" newLastName newFirstName
        adUser.Properties.["sAMAccountName"].Value <- newUserName
        adUser.Properties.["mail"].Value <- sprintf "%s.%s@%s" newLastName newFirstName config.MailDomain
        adUser.Properties.["proxyAddresses"].Value <- [ oldEMailAddress ] @ oldProxyAddresses @ proxyAddresses newFirstName newLastName config.MailDomain |> List.toArray
        let! newHomeDirectory = homePath (UserName newUserName) userType
        adUser.Properties.["homeDirectory"].Value <- newHomeDirectory

        // TODO handle if oldHomeDirectory doesn't exist
        if CIString oldHomeDirectory <> CIString newHomeDirectory then
            use! __ = createNetworkConnection oldHomeDirectory
            use! __ = createNetworkConnection newHomeDirectory
            Directory.Move(oldHomeDirectory, newHomeDirectory)

        match userType with
        | Teacher ->
            let! oldExercisePath = teacherExercisePath userName
            let! newExercisePath = teacherExercisePath (UserName newUserName)
            if CIString oldExercisePath <> CIString newExercisePath then
                use! __ = createNetworkConnection oldExercisePath
                use! __ = createNetworkConnection newExercisePath
                Directory.Move(oldExercisePath, newExercisePath)
        | Student _ -> ()
    })

let private setSokratesId userName userType (SokratesId sokratesId) =
    updateUser userName userType [||] (fun adUser -> reader {
        let! config = Reader.environment
        adUser.Properties.[config.SokratesIdAttributeName].Value <- sokratesId
    })

let private moveStudentToClass userName oldClassName newClassName =
    let oldUserType = Student oldClassName
    let newUserType = Student newClassName
    updateUser userName oldUserType [| "distinguishedName"; "homeDirectory" |] (fun adUser -> reader {
        let! targetOu = userRootEntry newUserType
        adUser.MoveTo(targetOu)

        adUser.Properties.["department"].Value <- departmentFromUserType newUserType
        let oldHomeDirectory = adUser.Properties.["homeDirectory"].Value :?> string
        let! newHomeDirectory = homePath userName newUserType
        adUser.Properties.["homeDirectory"].Value <- newHomeDirectory

        // TODO handle if oldHomeDirectory doesn't exist
        if CIString oldHomeDirectory <> CIString newHomeDirectory then
            use! __ = createNetworkConnection oldHomeDirectory
            use! __ = createNetworkConnection newHomeDirectory
            Directory.Move(oldHomeDirectory, newHomeDirectory)

        let distinguishedName = adUser.Properties.["distinguishedName"].Value :?> string
        do! updateGroup oldUserType [| "member" |] (fun adGroup -> reader { adGroup.Properties.["member"].Remove(distinguishedName) })
        do! updateGroup newUserType [| "member" |] (fun adGroup -> reader { adGroup.Properties.["member"].Add(distinguishedName) |> ignore })
    })

let private deleteUser userName userType = reader {
    use! adCtx = userRootEntry userType
    let searchResult = user adCtx userName [| "homeDirectory" |]

    use adUser = searchResult.GetDirectoryEntry()
    adUser.DeleteTree()

    match searchResult.Properties.["homeDirectory"] |> Seq.cast<string> |> Seq.tryItem 0 with
    | Some homeDirectory ->
        use! __ = createNetworkConnection homeDirectory
        Directory.delete homeDirectory
    | None -> ()

    match userType with
    | Teacher ->
        let! exercisePath = teacherExercisePath userName
        use! __ = createNetworkConnection exercisePath
        Directory.delete exercisePath
    | Student _ -> ()
}

let private createGroup userType = reader {
    do! reader {
        use! __ = userContainer userType |> Reader.bind (createOU >> Option.defaultWith (fun () -> failwithf "Error while creating OU for user type %A" userType))
        ()
    }

    do! reader {
        let! groupHomePath = groupHomePath userType
        use! __ = createNetworkConnection groupHomePath
        Directory.CreateDirectory(groupHomePath) |> ignore
    }

    let! groupPath = groupPathFromUserType userType
    let groupName = DN.head groupPath |> snd
    use! adGroup = createGroupEntry groupPath
    adGroup.Properties.["sAMAccountName"].Value <- groupName
    adGroup.Properties.["displayName"].Value <- groupName
    let! config = Reader.environment
    adGroup.Properties.["mail"].Value <- sprintf "%s@%s" groupName config.MailDomain
    adGroup.CommitChanges()

    match userType with
    | Student _ ->
        do! updateDirectoryEntry config.StudentGroup [| "member" |] (fun parentGroup -> reader {
            parentGroup.Properties.["member"].Add(adGroup.Properties.["distinguishedName"].Value) |> ignore
        })
    | Teacher -> ()
}

let private changeStudentGroupName (GroupName oldClassName) (GroupName newClassName) = reader {
    let oldUserType = Student (GroupName oldClassName)
    let newUserType = Student (GroupName newClassName)

    do! reader {
        use! adCtx = userContainer oldUserType |> Reader.bind (adDirectoryEntry [||])
        adCtx.Rename(sprintf "OU=%s" newClassName)
        adCtx.CommitChanges()
    }

    do! reader {
        let! oldGroupHomePath = groupHomePath oldUserType
        let! newGroupHomePath = groupHomePath newUserType
        use! __ = createNetworkConnection oldGroupHomePath
        use! __ = createNetworkConnection newGroupHomePath
        Directory.Move(oldGroupHomePath, newGroupHomePath)
    }

    do! updateGroup oldUserType [| "member" |] (fun adGroup -> reader {
        adGroup.Rename(sprintf "CN=%s" newClassName)
        adGroup.Properties.["sAMAccountName"].Value <- newClassName
        adGroup.Properties.["displayName"].Value <- newClassName
        let! config = Reader.environment
        adGroup.Properties.["mail"].Value <- sprintf "%s@%s" newClassName config.MailDomain

        do!
            adGroup.Properties.["member"]
            |> Seq.cast<string>
            |> Seq.map (DistinguishedName >> fun userPath ->
                updateDirectoryEntry userPath [| "sAMAccountName" |] (fun adUser -> reader {
                    adUser.Properties.["department"].Value <- departmentFromUserType newUserType
                    let userName = adUser.Properties.["sAMAccountName"].[0] :?> string |> UserName
                    let userHomePath = homePath userName newUserType
                    adUser.Properties.["homeDirectory"].Value <- userHomePath
                })
            )
            |> Reader.sequence
            |> Reader.ignore
    })
}

let private deleteGroup userType = reader {
    do! reader {
        use! adCtx = userRootEntry userType
        if adCtx.Children |> Seq.cast<DirectoryEntry> |> Seq.isEmpty |> not
        then failwith "Can't delete non-empty OU"
        adCtx.DeleteTree()
    }
    do! reader {
        let! groupHomePath = groupHomePath userType
        use! __ = createNetworkConnection groupHomePath
        try Directory.Delete(groupHomePath) with _ -> ()
    }
    do! reader {
        use! adCtx = groupPathFromUserType userType |> Reader.bind (adDirectoryEntry [||])
        adCtx.DeleteTree()
    }
}

let private getUserType teachers classGroups userName =
    if teachers |> Seq.contains userName then Some Teacher
    else
        classGroups
        |> Seq.tryPick (fun (groupName, members) ->
            if members |> Seq.contains userName then Some (Student groupName)
            else None
        )

let getUsers = reader {
    let! config = Reader.environment

    let! teachers = reader {
        let! adGroup = adDirectoryEntry [| "member" |] config.TeacherGroup
        return
            adGroup.Properties.["member"]
            |> Seq.cast<string>
            |> Seq.map DistinguishedName
            |> Seq.toList
    }

    let! classGroups = reader {
        let! studentGroup = adDirectoryEntry [| "member" |] config.StudentGroup
        return!
            studentGroup.Properties.["member"]
            |> Seq.cast<string>
            |> Seq.map (DistinguishedName >> fun groupName -> reader {
                use! group = adDirectoryEntry [| "sAMAccountName"; "member" |] groupName
                let groupName = group.Properties.["sAMAccountName"].Value :?> string |> GroupName
                let members = group.Properties.["member"] |> Seq.cast<string> |> Seq.map DistinguishedName |> Seq.toList
                return groupName, members
            })
            |> Reader.sequence
            |> Reader.map Seq.toList
    }

    return!
        [ config.TeacherContainer; config.ClassContainer ]
        |> List.map (fun userContainerPath -> reader {
            use! userCtx = adDirectoryEntry [||] userContainerPath
            let! config = Reader.environment
            use searcher = new DirectorySearcher(userCtx, "(&(objectCategory=person)(objectClass=user))", [| "distinguishedName"; "sAMAccountName"; "givenName"; "sn"; "whenCreated"; config.SokratesIdAttributeName |], PageSize = 1024)
            use searchResults = searcher.FindAll()
            return
                searchResults
                |> Seq.cast<SearchResult>
                |> Seq.choose (fun adUser ->
                    let distinguishedName = DistinguishedName (adUser.Properties.["distinguishedName"].[0] :?> string)
                    getUserType teachers classGroups distinguishedName
                    |> Option.map (fun userType ->
                        {
                            Name = UserName (adUser.Properties.["sAMAccountName"].[0] :?> string)
                            SokratesId = adUser.Properties.[config.SokratesIdAttributeName] |> Seq.cast<string> |> Seq.tryHead |> Option.map SokratesId
                            FirstName = adUser.Properties.["givenName"].[0] :?> string
                            LastName = adUser.Properties.["sn"].[0] :?> string
                            Type = userType
                            CreatedAt = adUser.Properties.["whenCreated"].[0] :?> DateTime
                        }
                    )
                )
                |> Seq.toList
        })
        |> Reader.sequence
        |> Reader.map (Seq.concat >> Seq.toList)
}

let getClassGroups = reader {
    let! config = Reader.environment
    use! studentGroup = adDirectoryEntry [| "member" |] config.StudentGroup
    return!
        studentGroup.Properties.["member"]
        |> Seq.cast<string>
        |> Seq.map (DistinguishedName >> fun groupName -> reader {
            use! group = adDirectoryEntry [| "sAMAccountName" |] groupName
            return group.Properties.["sAMAccountName"].Value :?> string |> GroupName
        })
        |> Reader.sequence
        |> Reader.map Seq.toList
}

let getComputers = reader {
    let! config = Reader.environment
    use! computerCtx = adDirectoryEntry [||] config.ComputerContainer
    use searcher = new DirectorySearcher(computerCtx, "(objectCategory=computer)", [| "dNSHostName" |], PageSize = 1024)
    use searchResults = searcher.FindAll()
    return
        searchResults
        |> Seq.cast<SearchResult>
        |> Seq.filter (fun adComputer -> adComputer.Properties.["dNSHostName"].Count > 0)
        |> Seq.choose (fun adComputer ->
            adComputer.Properties.["dNSHostName"] |> Seq.cast<string> |> Seq.tryExactlyOne
        )
        |> Seq.toList
}

let applyDirectoryModification = function
    | CreateUser (user, password) -> createUser user password
    | UpdateUser (userName, userType, ChangeUserName (newUserName, newFirstName, newLastName)) -> changeUserName userName userType (newUserName, newFirstName, newLastName)
    | UpdateUser (userName, userType, SetSokratesId sokratesId) -> setSokratesId userName userType sokratesId
    | UpdateUser (userName, Student oldClassName, MoveStudentToClass newClassName) -> moveStudentToClass userName oldClassName newClassName
    | UpdateUser (_, Teacher, MoveStudentToClass _) -> failwith "Can't move teacher to student class"
    | DeleteUser (userName, userType) -> deleteUser userName userType
    | CreateGroup (userType) -> createGroup userType
    | UpdateGroup (Teacher, ChangeGroupName _) -> failwith "Can't rename teacher group"
    | UpdateGroup (Student oldClassName, ChangeGroupName newClassName) -> changeStudentGroupName oldClassName newClassName
    | DeleteGroup userType -> deleteGroup userType

let applyDirectoryModifications =
    List.map (fun modification ->
        try
            applyDirectoryModification modification
        with e -> failwithf "Error while applying modification \"%A\": %O" modification e
    )
    >> Reader.sequence
    >> Reader.ignore
