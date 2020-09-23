module AD.Core

open AD.Domain
open CPI.DirectoryServices
open System
open System.DirectoryServices
open System.IO
open System.Security.AccessControl
open System.Security.Principal

// see http://www.gabescode.com/active-directory/2018/12/15/better-performance-activedirectory.html

type internal DistinguishedName = DistinguishedName of string

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

let private sokratesIdAttributeName = Environment.getEnvVarOrFail "AD_SOKRATES_ID_ATTRIBUTE_NAME"

let private serverIpAddress = Environment.getEnvVarOrFail "AD_SERVER"
let private adUserName = Environment.getEnvVarOrFail "AD_USER"
let private adPassword = Environment.getEnvVarOrFail "AD_PASSWORD"

let internal adDirectoryEntry properties (DistinguishedName path) =
    let entry = new DirectoryEntry(sprintf "LDAP://%s/%s" serverIpAddress path, adUserName, adPassword)
    entry.RefreshCache(properties)
    entry

let private groupHomePath userType =
    match userType with
    | Teacher -> Environment.getEnvVarOrFail "AD_TEACHER_HOME_PATH"
    | Student (GroupName className) -> Path.Combine(Environment.getEnvVarOrFail "AD_STUDENT_HOME_PATH", className)

let private homePath (UserName userName) userType =
    Path.Combine(groupHomePath userType, userName)

let private proxyAddresses firstName lastName mailDomain =
    [
        sprintf "smtp:%s.%s@%s" firstName lastName mailDomain
    ]

let private teacherExercisePath (UserName userName) =
    Path.Combine(Environment.getEnvVarOrFail "AD_TEACHER_EXERCISE_PATH", userName)

let private createOU path =
    path
    |> DN.parentsAndSelf
    |> Seq.rev
    |> Seq.filter DN.isOU
    |> Seq.map (fun path ->
        try
            let adCtx = adDirectoryEntry [||] path
            adCtx.Guid |> ignore // Test if OU exists
            adCtx
        with e ->
            let parentPath = DN.parent path
            use parentCtx = adDirectoryEntry [||] parentPath
            let child = parentCtx.Children.Add(uncurry (sprintf "%s=%s") (DN.head path), "organizationalUnit")
            child.CommitChanges()
            child
    )
    |> Seq.tryLast

let private createCN path entryType =
    let name = DN.tryCN path |> Option.defaultWith (fun () -> failwithf "Can't create CN entry: %A doesn't identify a CN entry" path)
    use parentCtx = createOU (DN.parent path) |> Option.defaultWith (fun () -> failwithf "Error while creating %O" path)
    let child = parentCtx.Children.Add(sprintf "CN=%s" name, entryType)
    parentCtx.CommitChanges()
    child

let private createGroupEntry path = createCN path "group"

let private teacherContainer = Environment.getEnvVarOrFail "AD_TEACHER_CONTAINER" |> DistinguishedName

let private classContainer = Environment.getEnvVarOrFail "AD_CLASS_CONTAINER" |> DistinguishedName

let private userContainer = function
    | Teacher -> teacherContainer
    | Student (GroupName className) -> classContainer |> DN.childOU className

let internal userRootEntry = userContainer >> adDirectoryEntry [||]

let internal user ctx (UserName userName) properties =
    use searcher = new DirectorySearcher(ctx, sprintf "(&(objectCategory=person)(objectClass=user)(sAMAccountName=%s))" userName, properties)
    searcher.FindOne()

let private teacherGroupPath = Environment.getEnvVarOrFail "AD_TEACHER_GROUP" |> DistinguishedName
let private teacherGroupName =
    DN.tryCN teacherGroupPath
    |> Option.defaultWith (fun () -> failwith "Can't get teacher group name: AD_TEACHER_GROUP must be a distinguished name with CN at the beginning")
    |> GroupName

let private studentGroupPath = Environment.getEnvVarOrFail "AD_STUDENT_GROUP" |> DistinguishedName
let private studentGroupName =
    DN.tryCN studentGroupPath
    |> Option.defaultWith (fun () -> failwith "Can't get student group name: AD_STUDENT_GROUP must be a distinguished name with CN at the beginning")
    |> GroupName

let private classGroupPath (GroupName className) = Environment.getEnvVarOrFail "AD_CLASS_GROUPS_CONTAINER" |> DistinguishedName |> DN.childCN className

let private testUserGroupPath = Environment.getEnvVarOrFail "AD_TEST_USER_GROUP" |> DistinguishedName

let internal groupPathFromUserType = function
    | Teacher -> teacherGroupPath
    | Student className -> classGroupPath className

let private departmentFromUserType = function
    | Teacher -> let (GroupName name) = teacherGroupName in name
    | Student (GroupName className) -> className

let private divisionFromUserType = function
    | Teacher -> let (GroupName name) = teacherGroupName in name
    | Student _ -> let (GroupName name) = studentGroupName in name

let private sidFromDirectoryEntry (directoryEntry: DirectoryEntry) =
    directoryEntry.RefreshCache([| "objectSid" |])
    let data = directoryEntry.Properties.["objectSid"].[0] :?> byte array
    SecurityIdentifier(data, 0)

let private sidFromDistinguishedName path =
    use adCtx = adDirectoryEntry [||] path
    sidFromDirectoryEntry adCtx

let private updateDirectoryEntry path properties fn =
    use adEntry = adDirectoryEntry properties path
    fn adEntry
    adEntry.CommitChanges()

let private updateGroup userType = updateDirectoryEntry (groupPathFromUserType userType)

let private updateUser userName userType properties fn =
    use adCtx = userRootEntry userType
    let searchResult = user adCtx userName properties
    use adUser = searchResult.GetDirectoryEntry()
    adUser.RefreshCache(properties)
    fn adUser
    adUser.CommitChanges()

let private createUser (newUser: NewUser) password =
    use adCtx = userRootEntry newUser.Type
    let (UserName userName) = newUser.Name
    let adUser = adCtx.Children.Add(sprintf "CN=%s" userName, "user")
    adCtx.CommitChanges()

    let mailDomain = Environment.getEnvVarOrFail "AD_MAIL_DOMAIN"
    let userPrincipalName = sprintf "%s@%s" userName mailDomain
    adUser.Properties.["userPrincipalName"].Value <- userPrincipalName
    newUser.SokratesId |> Option.iter (fun (SokratesId v) -> adUser.Properties.[sokratesIdAttributeName].Value <- v)
    adUser.Properties.["givenName"].Value <- newUser.FirstName
    adUser.Properties.["sn"].Value <- newUser.LastName
    adUser.Properties.["displayName"].Value <- sprintf "%s %s" newUser.LastName newUser.FirstName
    adUser.Properties.["sAMAccountName"].Value <- userName
    adUser.Properties.["department"].Value <- departmentFromUserType newUser.Type
    adUser.Properties.["division"].Value <- divisionFromUserType newUser.Type
    adUser.Properties.["mail"].Value <- sprintf "%s.%s@%s" newUser.LastName newUser.FirstName mailDomain
    adUser.Properties.["proxyAddresses"].Value <- proxyAddresses newUser.FirstName newUser.LastName mailDomain |> List.toArray
    let userHomePath = homePath newUser.Name newUser.Type
    adUser.Properties.["homeDirectory"].Value <- userHomePath
    adUser.Properties.["homeDrive"].Value <- Environment.getEnvVarOrFail "AD_HOME_DRIVE"
    adUser.Properties.["userAccountControl"].Value <- 0x220 // PASSWD_NOTREQD | NORMAL_ACCOUNT
    adUser.CommitChanges() // Must create user before setting password
    adUser.Invoke("SetPassword", [| password |]) |> ignore
    adUser.Properties.["pwdLastSet"].Value <- 0 // Expire password
    adUser.CommitChanges()

    updateGroup newUser.Type [| "member" |] (fun group ->
        group.Properties.["member"].Add(adUser.Properties.["distinguishedName"].Value) |> ignore
    )

    use __ = NetworkConnection.create adUserName adPassword userHomePath

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
        let exercisePath = teacherExercisePath newUser.Name

        let teacherSid = sidFromDistinguishedName teacherGroupPath
        let studentSid = sidFromDistinguishedName studentGroupPath
        let testUserSid = sidFromDistinguishedName testUserGroupPath

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

let private changeUserName userName userType (UserName newUserName, newFirstName, newLastName) =
    updateUser userName userType [| "mail"; "homeDirectory"; "proxyAddresses" |] (fun adUser ->
        let oldEMailAddress = adUser.Properties.["mail"].Value :?> string
        let oldHomeDirectory = adUser.Properties.["homeDirectory"].Value :?> string
        let oldProxyAddresses = adUser.Properties.["proxyAddresses"] |> Seq.cast<string> |> Seq.toList

        adUser.Rename(sprintf "CN=%s" newUserName)
        let mailDomain = Environment.getEnvVarOrFail "AD_MAIL_DOMAIN"
        adUser.Properties.["userPrincipalName"].Value <- sprintf "%s@%s" newUserName mailDomain
        adUser.Properties.["givenName"].Value <- newFirstName
        adUser.Properties.["sn"].Value <- newLastName
        adUser.Properties.["displayName"].Value <- sprintf "%s %s" newLastName newFirstName
        adUser.Properties.["sAMAccountName"].Value <- newUserName
        adUser.Properties.["mail"].Value <- sprintf "%s.%s@%s" newLastName newFirstName mailDomain
        adUser.Properties.["proxyAddresses"].Value <- [ oldEMailAddress ] @ oldProxyAddresses @ proxyAddresses newFirstName newLastName mailDomain |> List.toArray
        let newHomeDirectory = homePath (UserName newUserName) userType
        adUser.Properties.["homeDirectory"].Value <- newHomeDirectory

        // TODO handle if oldHomeDirectory doesn't exist
        if CIString oldHomeDirectory <> CIString newHomeDirectory then
            use __ = NetworkConnection.create adUserName adPassword oldHomeDirectory
            use __ = NetworkConnection.create adUserName adPassword newHomeDirectory
            Directory.Move(oldHomeDirectory, newHomeDirectory)

        match userType with
        | Teacher ->
            let oldExercisePath = teacherExercisePath userName
            let newExercisePath = teacherExercisePath (UserName newUserName)
            if CIString oldExercisePath <> CIString newExercisePath then
                use __ = NetworkConnection.create adUserName adPassword oldExercisePath
                use __ = NetworkConnection.create adUserName adPassword newExercisePath
                Directory.Move(oldExercisePath, newExercisePath)
        | Student _ -> ()
    )

let private setSokratesId userName userType (SokratesId sokratesId) =
    updateUser userName userType [||] (fun adUser ->
        adUser.Properties.[sokratesIdAttributeName].Value <- sokratesId
    )

let private moveStudentToClass userName oldClassName newClassName =
    let oldUserType = Student oldClassName
    let newUserType = Student newClassName
    updateUser userName oldUserType [| "distinguishedName"; "homeDirectory" |] (fun adUser ->
        let targetOu = userRootEntry newUserType
        adUser.MoveTo(targetOu)

        adUser.Properties.["department"].Value <- departmentFromUserType newUserType
        let oldHomeDirectory = adUser.Properties.["homeDirectory"].Value :?> string
        let newHomeDirectory = homePath userName newUserType
        adUser.Properties.["homeDirectory"].Value <- newHomeDirectory

        // TODO handle if oldHomeDirectory doesn't exist
        if CIString oldHomeDirectory <> CIString newHomeDirectory then
            use __ = NetworkConnection.create adUserName adPassword oldHomeDirectory
            use __ = NetworkConnection.create adUserName adPassword newHomeDirectory
            Directory.Move(oldHomeDirectory, newHomeDirectory)

        let distinguishedName = adUser.Properties.["distinguishedName"].Value :?> string
        updateGroup oldUserType [| "member" |] (fun adGroup -> adGroup.Properties.["member"].Remove(distinguishedName))
        updateGroup newUserType [| "member" |] (fun adGroup -> adGroup.Properties.["member"].Add(distinguishedName) |> ignore)
    )

let private deleteUser userName userType =
    use adCtx = userRootEntry userType
    let searchResult = user adCtx userName [| "homeDirectory" |]

    use adUser = searchResult.GetDirectoryEntry()
    adUser.DeleteTree()

    match searchResult.Properties.["homeDirectory"] |> Seq.cast<string> |> Seq.tryItem 0 with
    | Some homeDirectory ->
        use __ = NetworkConnection.create adUserName adPassword homeDirectory
        Directory.delete homeDirectory
    | None -> ()

    match userType with
    | Teacher ->
        let exercisePath = teacherExercisePath userName
        use __ = NetworkConnection.create adUserName adPassword exercisePath
        Directory.delete exercisePath
    | Student _ -> ()

let private createGroup userType =
    do
        use __ = userContainer userType |> createOU |> Option.defaultWith (fun () -> failwithf "Error while creating OU for user type %A" userType)
        ()

    do
        let groupHomePath = groupHomePath userType
        use __ = NetworkConnection.create adUserName adPassword groupHomePath
        Directory.CreateDirectory(groupHomePath) |> ignore

    let groupPath = groupPathFromUserType userType
    let groupName = DN.head groupPath |> snd
    use adGroup = createGroupEntry groupPath
    adGroup.Properties.["sAMAccountName"].Value <- groupName
    adGroup.Properties.["displayName"].Value <- groupName
    let mailDomain = Environment.getEnvVarOrFail "AD_MAIL_DOMAIN"
    adGroup.Properties.["mail"].Value <- sprintf "%s@%s" groupName mailDomain
    adGroup.CommitChanges()

    match userType with
    | Student _ ->
        updateDirectoryEntry studentGroupPath [| "member" |] (fun parentGroup ->
            parentGroup.Properties.["member"].Add(adGroup.Properties.["distinguishedName"].Value) |> ignore
        )
    | Teacher -> ()

let private changeStudentGroupName (GroupName oldClassName) (GroupName newClassName) =
    let oldUserType = Student (GroupName oldClassName)
    let newUserType = Student (GroupName newClassName)

    do
        use adCtx = userContainer oldUserType |> adDirectoryEntry [||]
        adCtx.Rename(sprintf "OU=%s" newClassName)
        adCtx.CommitChanges()

    do
        let oldGroupHomePath = groupHomePath oldUserType
        let newGroupHomePath = groupHomePath newUserType
        use __ = NetworkConnection.create adUserName adPassword oldGroupHomePath
        use __ = NetworkConnection.create adUserName adPassword newGroupHomePath
        Directory.Move(oldGroupHomePath, newGroupHomePath)

    updateGroup oldUserType [| "member" |] (fun adGroup ->
        adGroup.Rename(sprintf "CN=%s" newClassName)
        adGroup.Properties.["sAMAccountName"].Value <- newClassName
        adGroup.Properties.["displayName"].Value <- newClassName
        let mailDomain = Environment.getEnvVarOrFail "AD_MAIL_DOMAIN"
        adGroup.Properties.["mail"].Value <- sprintf "%s@%s" newClassName mailDomain

        adGroup.Properties.["member"]
        |> Seq.cast<string>
        |> Seq.map DistinguishedName
        |> Seq.iter (fun userPath ->
            updateDirectoryEntry userPath [| "sAMAccountName" |] (fun adUser ->
                adUser.Properties.["department"].Value <- departmentFromUserType newUserType
                let userName = adUser.Properties.["sAMAccountName"].[0] :?> string |> UserName
                let userHomePath = homePath userName newUserType
                adUser.Properties.["homeDirectory"].Value <- userHomePath
            )
        )
    )

let private deleteGroup userType =
    do
        use adCtx = userRootEntry userType
        if adCtx.Children |> Seq.cast<DirectoryEntry> |> Seq.isEmpty |> not
        then failwith "Can't delete non-empty OU"
        adCtx.DeleteTree()
    do
        let groupHomePath = groupHomePath userType
        use __ = NetworkConnection.create adUserName adPassword groupHomePath
        try Directory.Delete(groupHomePath) with _ -> ()
    do
        use adCtx = groupPathFromUserType userType |> adDirectoryEntry [||]
        adCtx.DeleteTree()

let private getUserType teachers classGroups userName =
    if teachers |> Seq.contains userName then Some Teacher
    else
        classGroups
        |> Seq.tryPick (fun (groupName, members) ->
            if members |> Seq.contains userName then Some (Student groupName)
            else None
        )

let getUsers () =
    let teachers =
        let adGroup = adDirectoryEntry [| "member" |] teacherGroupPath
        adGroup.Properties.["member"]
        |> Seq.cast<string>
        |> Seq.map DistinguishedName
        |> Seq.toList

    let classGroups =
        let studentGroup = adDirectoryEntry [| "member" |] studentGroupPath
        studentGroup.Properties.["member"]
        |> Seq.cast<string>
        |> Seq.map (DistinguishedName >> fun groupName ->
            use group = adDirectoryEntry [| "sAMAccountName"; "member" |] groupName
            let groupName = group.Properties.["sAMAccountName"].Value :?> string |> GroupName
            let members = group.Properties.["member"] |> Seq.cast<string> |> Seq.map DistinguishedName |> Seq.toList
            groupName, members
        )
        |> Seq.toList

    [ teacherContainer; classContainer ]
    |> List.collect (fun userContainerPath ->
        use userCtx = adDirectoryEntry [||] userContainerPath
        use searcher = new DirectorySearcher(userCtx, "(&(objectCategory=person)(objectClass=user))", [| "distinguishedName"; "sAMAccountName"; "givenName"; "sn"; "whenCreated"; sokratesIdAttributeName |], PageSize = 1024)
        use searchResults = searcher.FindAll()
        searchResults
        |> Seq.cast<SearchResult>
        |> Seq.choose (fun adUser ->
            let distinguishedName = DistinguishedName (adUser.Properties.["distinguishedName"].[0] :?> string)
            getUserType teachers classGroups distinguishedName
            |> Option.map (fun userType ->
                {
                    Name = UserName (adUser.Properties.["sAMAccountName"].[0] :?> string)
                    SokratesId = adUser.Properties.[sokratesIdAttributeName] |> Seq.cast<string> |> Seq.tryHead |> Option.map SokratesId
                    FirstName = adUser.Properties.["givenName"].[0] :?> string
                    LastName = adUser.Properties.["sn"].[0] :?> string
                    Type = userType
                    CreatedAt = adUser.Properties.["whenCreated"].[0] :?> DateTime
                }
            )
        )
        |> Seq.toList
    )

let getClassGroups () =
    use studentGroup = adDirectoryEntry [| "member" |] studentGroupPath
    studentGroup.Properties.["member"]
    |> Seq.cast<string>
    |> Seq.map (DistinguishedName >> fun groupName ->
        use group = adDirectoryEntry [| "sAMAccountName" |] groupName
        group.Properties.["sAMAccountName"].Value :?> string |> GroupName
    )
    |> Seq.toList

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
    List.iter (fun modification ->
        try
            applyDirectoryModification modification
        with e -> failwithf "Error while applying modification \"%A\": %O" modification e
    )
