module AD.BusinessLogic

open AD.DataTransferTypes
open System.DirectoryServices
open System.IO
open System.Security.AccessControl
open System.Security.Principal

// see http://www.gabescode.com/active-directory/2018/12/15/better-performance-activedirectory.html

type DistinguishedName = DistinguishedName of string

let private sokratesIdAttributeName = Environment.getEnvVarOrFail "AD_SOKRATES_ID_ATTRIBUTE_NAME"

let private serverIpAddress = Environment.getEnvVarOrFail "AD_SERVER"
let private adUserName = Environment.getEnvVarOrFail "AD_USER"
let private adPassword = Environment.getEnvVarOrFail "AD_PASSWORD"

let private adDirectoryEntry path =
    new DirectoryEntry(sprintf "LDAP://%s/%s" serverIpAddress path, adUserName, adPassword)

let private homePath (UserName userName) userType =
    match userType with
    | Teacher -> Environment.getEnvVarOrFail "AD_TEACHER_HOME_PATH" |> String.replace "<username>" userName
    | Student (GroupName className) -> Environment.getEnvVarOrFail "AD_STUDENT_HOME_PATH" |> String.replace "<username>" userName |> String.replace "<class>" className

let private proxyAddresses firstName lastName mailDomain =
    [
        sprintf "smtp:%s.%s@%s" firstName lastName mailDomain
    ]

let private teacherExercisePath (UserName userName) =
    Environment.getEnvVarOrFail "AD_TEACHER_EXERCISE_PATH" |> String.replace "<username>" userName

let private userContainer = function
    | Teacher -> Environment.getEnvVarOrFail "AD_TEACHER_CONTAINER"
    | Student (GroupName className) -> Environment.getEnvVarOrFail "AD_STUDENT_CONTAINER" |> String.replace "<class>" className

let private userRootEntry = userContainer >> adDirectoryEntry

let private user ctx (UserName userName) properties =
    use searcher = new DirectorySearcher(ctx, sprintf "(&(objectCategory=person)(objectClass=user)(sAMAccountName=%s))" userName, properties)
    searcher.FindOne()

let private group ctx (GroupName groupName) properties =
    use searcher = new DirectorySearcher(ctx, sprintf "(&(objectCategory=group)(sAMAccountName=%s))" groupName, properties)
    searcher.FindOne()

let private groupNameFromUserType = function
    | Teacher -> Environment.getEnvVarOrFail "AD_TEACHER_GROUP_NAME" |> GroupName
    | Student className -> className

let private objectSid (directoryEntry: DirectoryEntry) =
    let data = directoryEntry.Properties.["objectSid"].[0] :?> byte array
    SecurityIdentifier(data, 0)

let private groupSid groupName =
    use adCtx = adDirectoryEntry (Environment.getEnvVarOrFail "AD_GROUP_CONTAINER")
    let searchResult = group adCtx groupName [| "objectSid" |]
    let data = searchResult.Properties.["objectSid"].[0] :?> byte array
    SecurityIdentifier(data, 0)

let private updateUser userName userType properties fn =
    use adCtx = userRootEntry userType
    let searchResult = user adCtx userName properties
    use adUser = searchResult.GetDirectoryEntry()
    adUser.RefreshCache(properties)
    fn adUser
    adUser.CommitChanges()

let private updateGroup groupName properties fn =
    use adCtx = adDirectoryEntry (Environment.getEnvVarOrFail "AD_GROUP_CONTAINER")
    let searchResult = group adCtx groupName properties
    use adGroup = searchResult.GetDirectoryEntry()
    fn adGroup
    adGroup.CommitChanges()

let private createUser (newUser: User) password =
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

    updateGroup (groupNameFromUserType newUser.Type) [| "member" |] (fun group ->
        group.Properties.["member"].Add(adUser.Properties.["distinguishedName"].Value) |> ignore
    )

    use __ = NetworkConnection.create adUserName adPassword (Path.GetDirectoryName userHomePath)

    let adUserSid = objectSid adUser

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

        let teacherSid = groupSid (Environment.getEnvVarOrFail "AD_TEACHER_GROUP_NAME" |> GroupName)
        let studentSid = groupSid (Environment.getEnvVarOrFail "AD_STUDENT_GROUP_NAME" |> GroupName)
        let testUserSid = groupSid (Environment.getEnvVarOrFail "AD_TEST_USER_GROUP_NAME" |> GroupName)

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

        if not <| String.equalsCaseInsensitive oldHomeDirectory newHomeDirectory then
            use __ = NetworkConnection.create adUserName adPassword (Path.GetDirectoryName oldHomeDirectory)
            use __ = NetworkConnection.create adUserName adPassword (Path.GetDirectoryName newHomeDirectory)
            Directory.Move(oldHomeDirectory, newHomeDirectory)

        match userType with
        | Teacher ->
            let oldExercisePath = teacherExercisePath userName
            let newExercisePath = teacherExercisePath (UserName newUserName)
            if not <| String.equalsCaseInsensitive oldExercisePath newExercisePath then
                use __ = NetworkConnection.create adUserName adPassword (Path.GetDirectoryName oldExercisePath)
                use __ = NetworkConnection.create adUserName adPassword (Path.GetDirectoryName newExercisePath)
                Directory.Move(oldExercisePath, newExercisePath)
        | Student _ -> ()
    )

let private moveStudentToClass userName oldClassName newClassName =
    updateUser userName (Student oldClassName) [| "distinguishedName"; "homeDirectory" |] (fun adUser ->
        let targetOu = userRootEntry (Student newClassName)
        adUser.MoveTo(targetOu)

        let oldHomeDirectory = adUser.Properties.["homeDirectory"].Value :?> string
        let newHomeDirectory = homePath userName (Student newClassName)
        adUser.Properties.["homeDirectory"].Value <- newHomeDirectory

        if not <| String.equalsCaseInsensitive oldHomeDirectory newHomeDirectory then
            use __ = NetworkConnection.create adUserName adPassword (Path.GetDirectoryName oldHomeDirectory)
            use __ = NetworkConnection.create adUserName adPassword (Path.GetDirectoryName newHomeDirectory)
            Directory.Move(oldHomeDirectory, newHomeDirectory)

        let distinguishedName = adUser.Properties.["distinguishedName"].Value :?> string
        updateGroup oldClassName [| "member" |] (fun adGroup -> adGroup.Properties.["member"].Remove(distinguishedName))
        updateGroup newClassName [| "member" |] (fun adGroup -> adGroup.Properties.["member"].Add(distinguishedName) |> ignore)
    )

let private deleteUser userName userType =
    use adCtx = userRootEntry userType
    let searchResult = user adCtx userName [| "homeDirectory" |]
    let homeDirectory = searchResult.Properties.["homeDirectory"].[0] :?> string
    use adUser = searchResult.GetDirectoryEntry()
    adUser.DeleteTree()

    do
        use __ = NetworkConnection.create adUserName adPassword (Path.GetDirectoryName homeDirectory)
        Directory.Delete(homeDirectory, true)

    match userType with
    | Teacher ->
        let exercisePath = teacherExercisePath userName
        use __ = NetworkConnection.create adUserName adPassword (Path.GetDirectoryName exercisePath)
        Directory.Delete(exercisePath, true)
    | Student _ -> ()

let private createGroup userType members =
    let (GroupName groupName) = groupNameFromUserType userType
    use adCtx = adDirectoryEntry (Environment.getEnvVarOrFail "AD_GROUP_CONTAINER")
    let adGroup = adCtx.Children.Add(sprintf "CN=%s" groupName, "group")
    adCtx.CommitChanges()

    adGroup.Properties.["sAMAccountName"].Value <- groupName
    adGroup.Properties.["displayName"].Value <- groupName
    let mailDomain = Environment.getEnvVarOrFail "AD_MAIL_DOMAIN"
    adGroup.Properties.["mail"].Value <- sprintf "%s@%s" groupName mailDomain

    use adUserCtx = adDirectoryEntry (Environment.getEnvVarOrFail "AD_USER_CONTAINER")
    members
    |> List.iter (fun userName ->
        let searchResult = user adUserCtx userName [| "distinguishedName" |]
        adGroup.Properties.["member"].Add(searchResult.Properties.["distinguishedName"].[0]) |> ignore
    )

    adGroup.CommitChanges()

    match userType with
    | Student _ ->
        let parentGroupName = Environment.getEnvVarOrFail "AD_STUDENT_GROUP_NAME" |> GroupName
        updateGroup parentGroupName [| "member" |] (fun parentGroup ->
            parentGroup.Properties.["member"].Add(adGroup.Properties.["distinguishedName"].Value) |> ignore
        )
    | Teacher -> ()

let private changeGroupName userType (GroupName newGroupName) =
    let oldGroupName = groupNameFromUserType userType
    updateGroup oldGroupName [||] (fun adGroup ->
        adGroup.Rename(sprintf "CN=%s" newGroupName)
        adGroup.Properties.["sAMAccountName"].Value <- newGroupName
        adGroup.Properties.["displayName"].Value <- newGroupName
        let mailDomain = Environment.getEnvVarOrFail "AD_MAIL_DOMAIN"
        adGroup.Properties.["mail"].Value <- sprintf "%s@%s" newGroupName mailDomain
    )

let private deleteGroup userType =
    let groupName = groupNameFromUserType userType
    use adCtx = adDirectoryEntry (Environment.getEnvVarOrFail "AD_GROUP_CONTAINER")
    let searchResult = group adCtx groupName [||]
    use adGroup = searchResult.GetDirectoryEntry()
    adGroup.DeleteTree()

let private getUserType teachers classGroups userName =
    if teachers |> Seq.contains userName then Some Teacher
    else
        classGroups
        |> Seq.tryPick (fun (groupName, members) ->
            if members |> Seq.contains userName then Some (Student groupName)
            else None
        )

let getUsers () =
    use groupCtx = adDirectoryEntry (Environment.getEnvVarOrFail "AD_GROUP_CONTAINER")
    let teachers =
        let groupName = Environment.getEnvVarOrFail "AD_TEACHER_GROUP_NAME" |> GroupName
        let adGroup = group groupCtx groupName [| "member" |]
        adGroup.Properties.["member"]
        |> Seq.cast<string>
        |> Seq.map DistinguishedName
        |> Seq.toList
    let studentGroup =
        let groupName = Environment.getEnvVarOrFail "AD_STUDENT_GROUP_NAME" |> GroupName
        group groupCtx groupName [| "member" |]

    let classGroups =
        studentGroup.Properties.["member"]
        |> Seq.cast<string>
        |> Seq.map (fun groupName ->
            use group = adDirectoryEntry groupName
            group.RefreshCache([| "sAMAccountName"; "member" |])
            let groupName = group.Properties.["sAMAccountName"].Value :?> string |> GroupName
            let members = group.Properties.["member"] |> Seq.cast<string> |> Seq.map DistinguishedName |> Seq.toList
            groupName, members
        )
        |> Seq.toList

    use userCtx = adDirectoryEntry (Environment.getEnvVarOrFail "AD_USER_CONTAINER")
    use searcher = new DirectorySearcher(userCtx, "(&(objectCategory=person)(objectClass=user))", [| "distinguishedName"; "sAMAccountName"; "givenName"; "sn"; sokratesIdAttributeName |], PageSize = 1024)
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
            }
        )
    )
    |> Seq.toList

let applyDirectoryModification = function
    | CreateUser (user, password) -> createUser user password
    | UpdateUser (userName, userType, ChangeUserName (newUserName, newFirstName, newLastName)) -> changeUserName userName userType (newUserName, newFirstName, newLastName)
    | UpdateUser (userName, Student oldClassName, MoveStudentToClass newClassName) -> moveStudentToClass userName oldClassName newClassName
    | UpdateUser (_, Teacher, MoveStudentToClass _) -> failwith "Can't move teacher to student class"
    | DeleteUser (userName, userType) -> deleteUser userName userType
    | CreateGroup (userType, members) -> createGroup userType members
    | UpdateGroup (userType, ChangeGroupName newGroupName) ->  changeGroupName userType newGroupName
    | DeleteGroup userType -> deleteGroup userType
