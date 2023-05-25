namespace AD

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

type internal ADHelper(config) =
    member _.FetchDirectoryEntry properties (DistinguishedName dn) =
        let path = $"LDAP://%s{config.DomainControllerHostName}/%s{dn}"
        try
            let entry = new DirectoryEntry(path, config.UserName, config.Password)
            entry.RefreshCache(properties)
            entry
        with e ->
            let properties = String.concat ", " properties
            failwith $"Can't fetch AD entry \"%s{path}\" (Properties: \"%s{properties}\"): %s{e.Message}"

    member _.GetGroupHomePath userType =
        match userType with
        | Teacher -> config.TeacherHomePath
        | Student (GroupName className) -> Path.Combine(config.StudentHomePath, className)

    member x.GetUserHomePath (UserName userName) userType =
        let groupHomePath = x.GetGroupHomePath userType
        Path.Combine(groupHomePath, userName)

    member _.GetTeacherExercisePath (UserName userName) =
        Path.Combine(config.TeacherExercisePath, userName)

    member x.CreateOU path =
        path
        |> DN.parentsAndSelf
        |> Seq.rev
        |> Seq.filter DN.isOU
        |> Seq.map (fun path ->
            try
                let adCtx = x.FetchDirectoryEntry [||] path
                adCtx.Guid |> ignore // Test if OU exists
                adCtx
            with _ ->
                let parentPath = DN.parent path
                use parentCtx = x.FetchDirectoryEntry [||] parentPath
                let child = parentCtx.Children.Add(uncurry (sprintf "%s=%s") (DN.head path), "organizationalUnit")
                child.CommitChanges()
                child
        )
        |> Seq.tryLast

    member x.CreateCN path entryType =
        let name = DN.tryCN path |> Option.defaultWith (fun () -> failwithf "Can't create CN entry: %A doesn't identify a CN entry" path)
        use parentCtx = x.CreateOU (DN.parent path) |> Option.defaultWith (fun () -> failwithf "Error while creating %O" path)
        let child = parentCtx.Children.Add(sprintf "CN=%s" name, entryType)
        parentCtx.CommitChanges()
        child

    member x.CreateGroupEntry path = x.CreateCN path "group"

    member _.GetUserOu userType =
        match userType with
        | Teacher -> config.TeacherContainer
        | Student (GroupName className) -> DN.childOU className config.ClassContainer

    member x.FetchUserOu userType =
        x.GetUserOu userType
        |> x.FetchDirectoryEntry [||]

    member _.FindUser ctx (UserName userName) properties =
        use searcher = new DirectorySearcher(ctx, sprintf "(&(objectCategory=person)(objectClass=user)(sAMAccountName=%s))" userName, properties)
        searcher.FindOne()

    member _.FindUsers ctx properties =
        use searcher = new DirectorySearcher(ctx, "(&(objectCategory=person)(objectClass=user))", properties, PageSize = 1024)
        searcher.FindAll()

    member _.TeacherGroupName =
        DN.tryCN config.TeacherGroup
        |> Option.defaultWith (fun () -> failwith "Can't get teacher group name: AD:TeacherGroup must be a distinguished name with CN at the beginning")
        |> GroupName

    member _.StudentGroupName =
        DN.tryCN config.StudentGroup
        |> Option.defaultWith (fun () -> failwith "Can't get student group name: AD:StudentGroup must be a distinguished name with CN at the beginning")
        |> GroupName

    member _.GetGroupPathFromUserType userType =
        match userType with
        | Teacher -> config.TeacherGroup
        | Student (GroupName className) -> DN.childCN className config.ClassGroupsContainer

    member x.GetDepartmentFromUserType userType =
        match userType with
        | Teacher -> let (GroupName name) = x.TeacherGroupName in name
        | Student (GroupName className) -> className

    member x.GetDivisionFromUserType userType =
        match userType with
        | Teacher -> let (GroupName name) = x.TeacherGroupName in name
        | Student _ -> let (GroupName name) = x.StudentGroupName in name

    member _.FetchSid (directoryEntry: DirectoryEntry) =
        directoryEntry.RefreshCache([| "objectSid" |])
        let data = directoryEntry.Properties.["objectSid"].[0] :?> byte array
        SecurityIdentifier(data, 0)

    member x.FetchSid path =
        use adCtx = x.FetchDirectoryEntry [||] path
        x.FetchSid adCtx

    member x.UpdateDirectoryEntry path properties fn =
        use adEntry = x.FetchDirectoryEntry properties path
        fn adEntry
        adEntry.CommitChanges()

    member x.UpdateGroup userType properties fn =
        let path = x.GetGroupPathFromUserType userType
        x.UpdateDirectoryEntry path properties fn

    member x.UpdateUser userName userType properties fn =
        use adCtx = x.FetchUserOu userType
        let searchResult = x.FindUser adCtx userName properties
        use adUser = searchResult.GetDirectoryEntry()
        adUser.RefreshCache(properties)
        fn adUser
        adUser.CommitChanges()

    member _.FindComputers adCtx properties =
        use searcher = new DirectorySearcher(adCtx, "(objectCategory=computer)", properties, PageSize = 1024)
        searcher.FindAll()


type ADApi(config) =
    let adHelper = ADHelper(config)

    let createUser (newUser: NewUser) mailAliases (password: string) =
        use adCtx = adHelper.FetchUserOu newUser.Type
        let (UserName userName) = newUser.Name
        let adUser = adCtx.Children.Add(sprintf "CN=%s" userName, "user")
        adCtx.CommitChanges()

        let userPrincipalName = sprintf "%s@%s" userName config.MailDomain
        adUser.Properties.["userPrincipalName"].Value <- userPrincipalName
        newUser.SokratesId |> Option.iter (fun (SokratesId v) -> adUser.Properties.[config.SokratesIdAttributeName].Value <- v)
        adUser.Properties.["givenName"].Value <- newUser.FirstName
        adUser.Properties.["sn"].Value <- newUser.LastName
        adUser.Properties.["displayName"].Value <- sprintf "%s %s" newUser.LastName newUser.FirstName
        adUser.Properties.["sAMAccountName"].Value <- userName
        adUser.Properties.["department"].Value <- adHelper.GetDepartmentFromUserType newUser.Type
        adUser.Properties.["division"].Value <- adHelper.GetDivisionFromUserType newUser.Type
        adUser.Properties.["mail"].Value <- userPrincipalName
        let proxyAddresses =
            mailAliases
            |> List.map (MailAlias.toProxyAddress config.MailDomain >> ProxyAddress.toString)
            |> List.map (fun v -> v :> obj)
            |> List.toArray
        adUser.Properties.["proxyAddresses"].AddRange(proxyAddresses)
        let userHomePath = adHelper.GetUserHomePath newUser.Name newUser.Type
        adUser.Properties.["homeDirectory"].Value <- userHomePath
        adUser.Properties.["homeDrive"].Value <- config.HomeDrive
        adUser.Properties.["userAccountControl"].Value <- 0x220 // PASSWD_NOTREQD | NORMAL_ACCOUNT
        adUser.CommitChanges() // Must create user before setting password
        adUser.Invoke("SetPassword", [| password :> obj |]) |> ignore
        adUser.Properties.["pwdLastSet"].Value <- 0 // Expire password
        adUser.CommitChanges()

        adHelper.UpdateGroup newUser.Type [| "member" |] (fun group ->
            group.Properties.["member"].Add(adUser.Properties.["distinguishedName"].Value) |> ignore
        )

        use __ = NetworkConnection.create config.NetworkShareUser config.NetworkSharePassword userHomePath

        let adUserSid = adHelper.FetchSid adUser

        do
            let dir = Directory.CreateDirectory(userHomePath)
            let acl = dir.GetAccessControl()
            acl.SetAccessRuleProtection(true, false) // Disable inheritance
            acl.AddAccessRule(FileSystemAccessRule(SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
            acl.AddAccessRule(FileSystemAccessRule(adUserSid, FileSystemRights.Modify, InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow))
            dir.SetAccessControl(acl)

        match newUser.Type with
        | Teacher ->
            let exercisePath = adHelper.GetTeacherExercisePath newUser.Name

            let teacherSid = adHelper.FetchSid config.TeacherGroup
            let studentSid = adHelper.FetchSid config.StudentGroup
            let testUserSid = adHelper.FetchSid config.TestUserGroup

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

    let changeUserName userName userType (UserName newUserName, newFirstName, newLastName, newMailAliasNames) =
        adHelper.UpdateUser userName userType [| "homeDirectory" |] (fun adUser ->
            let oldHomeDirectory = adUser.Properties.["homeDirectory"].Value :?> string

            adUser.Rename(sprintf "CN=%s" newUserName)
            adUser.Properties.["userPrincipalName"].Value <- sprintf "%s@%s" newUserName config.MailDomain
            adUser.Properties.["givenName"].Value <- newFirstName
            adUser.Properties.["sn"].Value <- newLastName
            adUser.Properties.["displayName"].Value <- sprintf "%s %s" newLastName newFirstName
            adUser.Properties.["sAMAccountName"].Value <- newUserName
            adUser.Properties.["mail"].Value <- sprintf "%s.%s@%s" (String.asAlphaNumeric newLastName) (String.asAlphaNumeric newFirstName) config.MailDomain
            adUser.Properties.["proxyAddresses"].Value <- newMailAliasNames |> List.map (MailAlias.toProxyAddress config.MailDomain >> ProxyAddress.toString) |> List.toArray
            let newHomeDirectory = adHelper.GetUserHomePath (UserName newUserName) userType
            adUser.Properties.["homeDirectory"].Value <- newHomeDirectory

            // TODO handle if oldHomeDirectory doesn't exist
            if CIString oldHomeDirectory <> CIString newHomeDirectory then
                use __ = NetworkConnection.create config.NetworkShareUser config.NetworkSharePassword oldHomeDirectory
                use __ = NetworkConnection.create config.NetworkShareUser config.NetworkSharePassword newHomeDirectory
                Directory.Move(oldHomeDirectory, newHomeDirectory)

            match userType with
            | Teacher ->
                let oldExercisePath = adHelper.GetTeacherExercisePath userName
                let newExercisePath = adHelper.GetTeacherExercisePath (UserName newUserName)
                if CIString oldExercisePath <> CIString newExercisePath then
                    use __ = NetworkConnection.create config.NetworkShareUser config.NetworkSharePassword oldExercisePath
                    use __ = NetworkConnection.create config.NetworkShareUser config.NetworkSharePassword newExercisePath
                    Directory.Move(oldExercisePath, newExercisePath)
            | Student _ -> ()
        )

    let setSokratesId userName userType (SokratesId sokratesId) =
        adHelper.UpdateUser userName userType [||] (fun adUser ->
            adUser.Properties.[config.SokratesIdAttributeName].Value <- sokratesId
        )

    let moveStudentToClass userName oldClassName newClassName =
        let oldUserType = Student oldClassName
        let newUserType = Student newClassName
        adHelper.UpdateUser userName oldUserType [| "distinguishedName"; "homeDirectory" |] (fun adUser ->
            let targetOu = adHelper.FetchUserOu newUserType
            adUser.MoveTo(targetOu)

            let department = adHelper.GetDepartmentFromUserType newUserType
            adUser.Properties.["department"].Value <- department
            let oldHomeDirectory = adUser.Properties.["homeDirectory"].Value :?> string
            let newHomeDirectory = adHelper.GetUserHomePath userName newUserType
            adUser.Properties.["homeDirectory"].Value <- newHomeDirectory

            // TODO handle if oldHomeDirectory doesn't exist
            if CIString oldHomeDirectory <> CIString newHomeDirectory then
                use __ = NetworkConnection.create config.NetworkShareUser config.NetworkSharePassword oldHomeDirectory
                use __ = NetworkConnection.create config.NetworkShareUser config.NetworkSharePassword newHomeDirectory
                Directory.Move(oldHomeDirectory, newHomeDirectory)

            let distinguishedName = adUser.Properties.["distinguishedName"].Value :?> string
            adHelper.UpdateGroup oldUserType [| "member" |] (fun adGroup -> adGroup.Properties.["member"].Remove(distinguishedName))
            adHelper.UpdateGroup newUserType [| "member" |] (fun adGroup -> adGroup.Properties.["member"].Add(distinguishedName) |> ignore)
        )

    let deleteUser userName userType =
        use adCtx = adHelper.FetchUserOu userType
        let searchResult = adHelper.FindUser adCtx userName [| "homeDirectory" |]

        match searchResult.Properties.["homeDirectory"] |> Seq.cast<string> |> Seq.tryItem 0 with
        | Some homeDirectory ->
            use __ = NetworkConnection.create config.NetworkShareUser config.NetworkSharePassword homeDirectory
            Directory.delete homeDirectory
        | None -> ()

        match userType with
        | Teacher ->
            let exercisePath = adHelper.GetTeacherExercisePath userName
            use __ = NetworkConnection.create config.NetworkShareUser config.NetworkSharePassword exercisePath
            Directory.delete exercisePath
        | Student _ -> ()

        use adUser = searchResult.GetDirectoryEntry()
        adUser.DeleteTree()

    let createGroup userType =
        do
            use __ = adHelper.GetUserOu userType |> adHelper.CreateOU |> Option.defaultWith (fun () -> failwithf "Error while creating OU for user type %A" userType)
            ()

        do
            let groupHomePath = adHelper.GetGroupHomePath userType
            use __ = NetworkConnection.create config.NetworkShareUser config.NetworkSharePassword groupHomePath
            Directory.CreateDirectory(groupHomePath) |> ignore

        let groupPath = adHelper.GetGroupPathFromUserType userType
        let groupName = DN.head groupPath |> snd
        use adGroup = adHelper.CreateGroupEntry groupPath
        adGroup.Properties.["sAMAccountName"].Value <- groupName
        adGroup.Properties.["displayName"].Value <- groupName
        adGroup.Properties.["mail"].Value <- sprintf "%s@%s" groupName config.MailDomain
        adGroup.CommitChanges()

        match userType with
        | Student _ ->
            adHelper.UpdateDirectoryEntry config.StudentGroup [| "member" |] (fun parentGroup ->
                parentGroup.Properties.["member"].Add(adGroup.Properties.["distinguishedName"].Value) |> ignore
            )
        | Teacher -> ()

    let changeStudentGroupName (GroupName oldClassName) (GroupName newClassName) =
        let oldUserType = Student (GroupName oldClassName)
        let newUserType = Student (GroupName newClassName)

        do
            use adCtx = adHelper.GetUserOu oldUserType |> adHelper.FetchDirectoryEntry [||]
            adCtx.Rename(sprintf "OU=%s" newClassName)
            adCtx.CommitChanges()

        do
            let oldGroupHomePath = adHelper.GetGroupHomePath oldUserType
            let newGroupHomePath = adHelper.GetGroupHomePath newUserType
            use __ = NetworkConnection.create config.NetworkShareUser config.NetworkSharePassword oldGroupHomePath
            use __ = NetworkConnection.create config.NetworkShareUser config.NetworkSharePassword newGroupHomePath
            Directory.Move(oldGroupHomePath, newGroupHomePath)

        adHelper.UpdateGroup oldUserType [| "member" |] (fun adGroup ->
            adGroup.Rename(sprintf "CN=%s" newClassName)
            adGroup.Properties.["sAMAccountName"].Value <- newClassName
            adGroup.Properties.["displayName"].Value <- newClassName
            adGroup.Properties.["mail"].Value <- sprintf "%s@%s" newClassName config.MailDomain

            adGroup.Properties.["member"]
            |> Seq.cast<string>
            |> Seq.iter (DistinguishedName >> fun userPath ->
                adHelper.UpdateDirectoryEntry userPath [| "sAMAccountName" |] (fun adUser ->
                    adUser.Properties.["department"].Value <- adHelper.GetDepartmentFromUserType newUserType
                    let userName = adUser.Properties.["sAMAccountName"].[0] :?> string |> UserName
                    adUser.Properties.["homeDirectory"].Value <- adHelper.GetUserHomePath userName newUserType
                )
            )
        )

    let deleteGroup userType =
        do
            use adCtx = adHelper.FetchUserOu userType
            if adCtx.Children |> Seq.cast<DirectoryEntry> |> Seq.isEmpty |> not
            then failwith "Can't delete non-empty OU"
            adCtx.DeleteTree()

        do
            let groupHomePath = adHelper.GetGroupHomePath userType
            use __ = NetworkConnection.create config.NetworkShareUser config.NetworkSharePassword groupHomePath
            try Directory.Delete(groupHomePath) with _ -> ()

        do
            use adCtx = adHelper.GetGroupPathFromUserType userType |> adHelper.FetchDirectoryEntry [||]
            adCtx.DeleteTree()

    let getUserType teachers classGroups userName =
        if teachers |> Seq.contains userName then Some Teacher
        else
            classGroups
            |> Seq.tryPick (fun (groupName, members) ->
                if members |> Seq.contains userName then Some (Student groupName)
                else None
            )

    let userProperties =
        [|
            "distinguishedName"
            "sAMAccountName"
            "givenName"
            "sn"
            "whenCreated"
            "mail"
            "proxyAddresses"
            "userPrincipalName"
            config.SokratesIdAttributeName
        |]

    let userFromADUserSearchResult userType sokratesIdAttributeName (adUser: SearchResult) =
        {
            Name = UserName (adUser.Properties.["sAMAccountName"].[0] :?> string)
            SokratesId = adUser.Properties.[sokratesIdAttributeName] |> Seq.cast<string> |> Seq.tryHead |> Option.map SokratesId
            FirstName = adUser.Properties.["givenName"].[0] :?> string
            LastName = adUser.Properties.["sn"].[0] :?> string
            Type = userType
            CreatedAt = adUser.Properties.["whenCreated"].[0] :?> DateTime
            Mail = adUser.Properties.["mail"] |> Seq.cast<string> |> Seq.choose MailAddress.tryParse |> Seq.tryHead
            ProxyAddresses =
                adUser.Properties.["proxyAddresses"]
                |> Seq.cast<string>
                |> Seq.choose ProxyAddress.tryParse
                |> Seq.toList
            UserPrincipalName = adUser.Properties.["userPrincipalName"].[0] :?> string |> (fun v -> MailAddress.tryParse v |> Option.defaultWith (fun () -> failwithf "Can't parse user principal name \"%s\" as mail address (User \"%s\")" v adUser.Path))
        }

    let applyDirectoryModification = function
        | CreateUser (user, mailAliases, password) -> createUser user mailAliases password
        | UpdateUser (userName, userType, ChangeUserName (newUserName, newFirstName, newLastName, newMailAliasNames)) -> changeUserName userName userType (newUserName, newFirstName, newLastName, newMailAliasNames)
        | UpdateUser (userName, userType, SetSokratesId sokratesId) -> setSokratesId userName userType sokratesId
        | UpdateUser (userName, Student oldClassName, MoveStudentToClass newClassName) -> moveStudentToClass userName oldClassName newClassName
        | UpdateUser (_, Teacher, MoveStudentToClass _) -> failwith "Can't move teacher to student class"
        | DeleteUser (userName, userType) -> deleteUser userName userType
        | CreateGroup (userType) -> createGroup userType
        | UpdateGroup (Teacher, ChangeGroupName _) -> failwith "Can't rename teacher group"
        | UpdateGroup (Student oldClassName, ChangeGroupName newClassName) -> changeStudentGroupName oldClassName newClassName
        | DeleteGroup userType -> deleteGroup userType

    member _.GetUsers () =
        let teachers =
            let adGroup = adHelper.FetchDirectoryEntry [| "member" |] config.TeacherGroup
            adGroup.Properties.["member"]
            |> Seq.cast<string>
            |> Seq.map DistinguishedName
            |> Seq.toList

        let classGroups =
            let studentGroup = adHelper.FetchDirectoryEntry [| "member" |] config.StudentGroup
            studentGroup.Properties.["member"]
            |> Seq.cast<string>
            |> Seq.map (DistinguishedName >> fun groupName ->
                use group = adHelper.FetchDirectoryEntry [| "sAMAccountName"; "member" |] groupName
                let groupName = group.Properties.["sAMAccountName"].Value :?> string |> GroupName
                let members = group.Properties.["member"] |> Seq.cast<string> |> Seq.map DistinguishedName |> Seq.toList
                (groupName, members)
            )
            |> Seq.toList

        [ config.TeacherContainer; config.ClassContainer ]
        |> List.collect (fun userContainerPath ->
            use userCtx = adHelper.FetchDirectoryEntry [||] userContainerPath
            use searchResults = adHelper.FindUsers userCtx userProperties
            searchResults
            |> Seq.cast<SearchResult>
            |> Seq.choose (fun adUser ->
                let distinguishedName = DistinguishedName (adUser.Properties.["distinguishedName"].[0] :?> string)
                getUserType teachers classGroups distinguishedName
                |> Option.map (fun userType ->
                    userFromADUserSearchResult userType config.SokratesIdAttributeName adUser
                )
            )
            |> Seq.toList
        )

    member _.GetUsers userType =
        use adCtx = adHelper.FetchUserOu userType
        let userProperties = userProperties
        use searchResult = adHelper.FindUsers adCtx userProperties
        searchResult
        |> Seq.cast<SearchResult>
        |> Seq.map (userFromADUserSearchResult userType config.SokratesIdAttributeName)
        |> Seq.toList

    member _.GetUser userName userType =
        use adCtx = adHelper.FetchUserOu userType
        let userProperties = userProperties
        let adUser = adHelper.FindUser adCtx userName userProperties
        userFromADUserSearchResult userType config.SokratesIdAttributeName adUser

    member _.GetClassGroups () =
        use studentGroup = adHelper.FetchDirectoryEntry [| "member" |] config.StudentGroup
        studentGroup.Properties.["member"]
        |> Seq.cast<string>
        |> Seq.map (DistinguishedName >> fun groupName ->
            use group = adHelper.FetchDirectoryEntry [| "sAMAccountName" |] groupName
            group.Properties.["sAMAccountName"].Value :?> string |> GroupName
        )
        |> Seq.toList

    member _.GetComputers () =
        use adCtx = adHelper.FetchDirectoryEntry [||] config.ComputerContainer
        use searchResults = adHelper.FindComputers adCtx [| "dNSHostName" |]
        searchResults
        |> Seq.cast<SearchResult>
        |> Seq.filter (fun adComputer -> adComputer.Properties.["dNSHostName"].Count > 0)
        |> Seq.choose (fun adComputer ->
            adComputer.Properties.["dNSHostName"] |> Seq.cast<string> |> Seq.tryExactlyOne
        )
        |> Seq.toList

    member _.ApplyDirectoryModifications =
        List.iter (fun modification ->
            try
                applyDirectoryModification modification
            with e -> failwithf "Error while applying modification \"%A\": %O" modification e
        )

    static member FromEnvironment () =
        ADApi(Config.fromEnvironment ())

