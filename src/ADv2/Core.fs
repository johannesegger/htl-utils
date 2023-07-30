module AD.Core

open AD.Configuration
open AD.Directory
open AD.Domain
open AD.Ldap
open AD.Operations
open System.IO
open System.Text.RegularExpressions

type ADApi(config: Config) =
    let getGroupHomePath userType =
        match userType with
        | Teacher -> config.Properties.TeacherHomePath
        | Student (GroupName className) -> Path.Combine(config.Properties.StudentHomePath, className)

    let getUserHomePath (UserName userName) userType =
        let groupHomePath = getGroupHomePath userType
        Path.Combine(groupHomePath, userName)

    let getTeacherExercisePath basePath (UserName userName) =
        Path.Combine(basePath, userName)

    let getUserContainer userType =
        match userType with
        | Teacher -> config.Properties.TeacherContainer
        | Student (GroupName className) -> DN.childOU className config.Properties.ClassContainer

    let getUserPrincipalName (UserName userName) = $"%s{userName}@%s{config.Properties.MailDomain}"

    let teacherGroupName =
        DN.tryCN config.Properties.TeacherGroup
        |> Option.defaultWith (fun () -> failwith "Can't get teacher group name: AD_TEACHER_GROUP must be a distinguished name with CN at the beginning")
        |> GroupName

    let studentGroupName =
        DN.tryCN config.Properties.StudentGroup
        |> Option.defaultWith (fun () -> failwith "Can't get student group name: AD_STUDENT_GROUP must be a distinguished name with CN at the beginning")
        |> GroupName

    let getGroupPathFromUserType userType =
        match userType with
        | Teacher -> config.Properties.TeacherGroup
        | Student (GroupName className) -> DN.childCN className config.Properties.ClassGroupsContainer

    let getDepartmentFromUserType = function
        | Teacher -> let (GroupName name) = teacherGroupName in name
        | Student (GroupName className) -> className

    let getDivisionFromUserType = function
        | Teacher -> let (GroupName name) = teacherGroupName in name
        | Student _ -> let (GroupName name) = studentGroupName in name

    let createUser (newUser: NewUser) =
        let parentNode = getUserContainer newUser.Type
        let userPrincipalName = getUserPrincipalName newUser.Name
        let department = getDepartmentFromUserType newUser.Type
        let division = getDivisionFromUserType newUser.Type
        let userHomePath = getUserHomePath newUser.Name newUser.Type
        let userDn = (let (UserName userName) = newUser.Name in DN.childCN userName parentNode)
        let groupDn = getGroupPathFromUserType newUser.Type
        [
            yield CreateNode
                {|
                    Node = userDn
                    NodeType = ADUser
                    Properties = [
                        yield "userPrincipalName", Text userPrincipalName
                        yield! newUser.SokratesId |> Option.map (fun (SokratesId v) -> config.Properties.SokratesIdAttributeName, Text v) |> Option.toList
                        yield "givenName", Text newUser.FirstName
                        yield "sn", Text newUser.LastName
                        yield "displayName", Text (sprintf "%s %s" newUser.LastName newUser.FirstName)
                        yield "sAMAccountName", (let (UserName userName) = newUser.Name in Text userName)
                        yield "department", Text department
                        yield "division", Text division
                        yield "mail", Text userPrincipalName
                        yield "proxyAddresses", newUser.MailAliases |> List.map (MailAlias.toProxyAddress config.Properties.MailDomain >> ProxyAddress.toString) |> TextList
                        yield "homeDirectory", Text userHomePath
                        yield "homeDrive", Text config.Properties.HomeDrive
                        yield "userAccountControl", Text (sprintf "%d" 0x220) // PASSWD_NOTREQD | NORMAL_ACCOUNT (see https://docs.microsoft.com/en-us/troubleshoot/windows-server/identity/useraccountcontrol-manipulate-account-properties)
                        yield "pwdLastSet", Text "0" // Expire password so the user must change it after first logon
                        yield "unicodePwd", Bytes (AD.password newUser.Password)
                    ]
                |}
            yield AddObjectToGroup {| Object = userDn; Group = groupDn |}
            yield CreateUserHomePath userDn
            match newUser.Type with
            | Teacher ->
                yield CreateExercisePath {|
                    Teacher = userDn
                    Path = getTeacherExercisePath config.Properties.TeacherExercisePath newUser.Name
                    Groups = {|
                        Teachers = config.Properties.TeacherGroup
                        Students = config.Properties.StudentGroup
                        TestUsers = config.Properties.TestUserGroup
                    |}
                |}
            | Student _ -> ()
        ]

    let changeUserName userName userType (newUserName, newFirstName, newLastName, newMailAliases) =
        let parentNode = getUserContainer userType
        let userPrincipalName = getUserPrincipalName newUserName
        let oldUserDn = (let (UserName userName) = userName in DN.childCN userName parentNode)
        let newUserDn = (let (UserName userName) = newUserName in DN.childCN userName parentNode)
        let homePath = getUserHomePath newUserName userType
        [
            yield MoveNode {| Source = oldUserDn; Target = newUserDn |}
            yield SetNodeProperties
                {|
                    Node = newUserDn
                    Properties = [
                        "userPrincipalName", Text userPrincipalName
                        "givenName", Text newFirstName
                        "sn", Text newLastName
                        "displayName", Text (sprintf "%s %s" newLastName newFirstName)
                        "sAMAccountName", (let (UserName userName) = newUserName in Text userName)
                        "mail", Text userPrincipalName
                        "proxyAddresses", newMailAliases |> List.map (MailAlias.toProxyAddress config.Properties.MailDomain >> ProxyAddress.toString) |> TextList
                    ]
                |}
            match userType with
            | Teacher ->
                yield MoveDirectory
                    {|
                        Source = getTeacherExercisePath config.Properties.TeacherExercisePath userName
                        Target = getTeacherExercisePath config.Properties.TeacherExercisePath newUserName
                    |}
            | Student _ -> ()
            yield MoveUserHomePath {| User = newUserDn; HomePath = homePath |}
        ]

    let setSokratesId userName userType (SokratesId sokratesId) =
        let parentNode = getUserContainer userType
        let userDn = (let (UserName userName) = userName in DN.childCN userName parentNode)
        [
            SetNodeProperties
                {|
                    Node = userDn
                    Properties = [
                        config.Properties.SokratesIdAttributeName, Text sokratesId
                    ]
                |}
        ]

    let moveStudentToClass userName oldClassName newClassName =
        let oldGroup = getGroupPathFromUserType (Student oldClassName)
        let newGroup = getGroupPathFromUserType (Student newClassName)
        let oldParentNode = getUserContainer (Student oldClassName)
        let newParentNode = getUserContainer (Student newClassName)
        let oldUserDn = (let (UserName userName) = userName in DN.childCN userName oldParentNode)
        let newUserDn = (let (UserName userName) = userName in DN.childCN userName newParentNode)
        let homePath = getUserHomePath userName (Student newClassName)
        [
            RemoveObjectFromGroup {| Object = oldUserDn; Group = oldGroup |}
            MoveNode {| Source = oldUserDn; Target = newUserDn |}
            AddObjectToGroup {| Object = newUserDn; Group = newGroup |}
            MoveUserHomePath {| User = newUserDn; HomePath = homePath |}
        ]

    let deleteUser userName userType =
        let parentNode = getUserContainer userType
        let userDn = (let (UserName userName) = userName in DN.childCN userName parentNode)
        [
            yield DeleteUserHomePath userDn
            match userType with
            | Teacher ->
                yield DeleteDirectory (getTeacherExercisePath config.Properties.TeacherExercisePath userName)
            | Student _ -> ()
            yield DeleteNode userDn
        ]

    let createGroup userType =
        let ouDn = getUserContainer userType
        let groupDn = getGroupPathFromUserType userType
        let groupName = DN.head groupDn |> snd
        let groupHomePath = getGroupHomePath userType
        [
            yield CreateNode
                {|
                    Node = ouDn
                    NodeType = ADOrganizationalUnit
                    Properties = []
                |}
            yield CreateGroupHomePath groupHomePath
            yield CreateNode
                {|
                    Node = groupDn
                    NodeType = ADGroup
                    Properties = [
                        "sAMAccountName", Text groupName
                        "displayName", Text groupName
                        "mail", Text $"%s{groupName}@%s{config.Properties.MailDomain}"
                    ]
                |}
            match userType with
            | Student _ ->
                yield AddObjectToGroup
                    {|
                        Object = groupDn
                        Group = config.Properties.StudentGroup
                    |}
            | Teacher -> ()
        ]

    let changeStudentGroupName oldClassName newClassName =
        let oldOuDn = getUserContainer (Student oldClassName)
        let newOuDn = getUserContainer (Student newClassName)
        let oldGroupDn = getGroupPathFromUserType (Student oldClassName)
        let newGroupDn = getGroupPathFromUserType (Student newClassName)
        let oldGroupHomePath = getGroupHomePath (Student oldClassName)
        let newGroupHomePath = getGroupHomePath (Student newClassName)
        let department = getDepartmentFromUserType (Student newClassName)
        [
            MoveNode {| Source = oldOuDn; Target = newOuDn |}
            MoveNode {| Source = oldGroupDn; Target = newGroupDn |}
            SetNodeProperties
                {|
                    Node = newGroupDn
                    Properties = [
                        "sAMAccountName", (let (GroupName groupName) = newClassName in Text groupName)
                        "displayName", (let (GroupName groupName) = newClassName in Text groupName)
                        "mail", (let (GroupName groupName) = newClassName in Text (sprintf "%s@%s" groupName config.Properties.MailDomain))
                    ]
                |}
            MoveDirectory {| Source = oldGroupHomePath; Target = newGroupHomePath |}
            ForEachGroupMember
                {|
                    Group = newGroupDn
                    Operations = fun userDn -> [
                        SetNodeProperties
                            {|
                                Node = userDn
                                Properties = [
                                    "department", Text department
                                ]
                            |}
                        ReplaceTextInNodePropertyValues
                            {|
                                Node = userDn
                                Properties = [
                                    {| Name = "homeDirectory"; Pattern = Regex($"^{Regex.Escape(oldGroupHomePath)}"); Replacement = newGroupHomePath |}
                                ]
                            |}
                    ]
                |}
        ]

    let deleteGroup userType =
        let ouDn = getUserContainer userType
        let groupDn = getGroupPathFromUserType userType
        let groupHomePath = getGroupHomePath userType
        [
            DeleteNode ouDn
            DeleteNode groupDn
            DeleteDirectory groupHomePath
        ]

    let tryGetUserType teachers classGroups (userName: DistinguishedName) =
        if teachers |> Seq.contains userName then Some Teacher
        else
            classGroups
            |> Seq.tryPick (fun (groupName, members) ->
                if members |> Seq.contains userName then Some (Student groupName)
                else None
            )

    let userProperties = [|
        //"distinguishedName"
        "sAMAccountName"
        "givenName"
        "sn"
        "whenCreated"
        "mail"
        "proxyAddresses"
        "userPrincipalName"
        config.Properties.SokratesIdAttributeName
    |]

    let getUserFromSearchResult userType adUser =
        {
            Name =
                adUser
                |> SearchResultEntry.getStringAttributeValue "sAMAccountName"
                |> UserName
            SokratesId =
                adUser
                |> SearchResultEntry.getOptionalStringAttributeValue config.Properties.SokratesIdAttributeName
                |> Option.map SokratesId
            FirstName = SearchResultEntry.getStringAttributeValue "givenName" adUser
            LastName = SearchResultEntry.getStringAttributeValue "sn" adUser
            Type = userType
            CreatedAt = SearchResultEntry.getDateTimeAttributeValue "whenCreated" adUser
            Mail = SearchResultEntry.getOptionalStringAttributeValue "mail" adUser
            ProxyAddresses =
                adUser
                |> SearchResultEntry.getStringAttributeValues "proxyAddresses"
                |> List.map (fun v ->
                    ProxyAddress.tryParse v
                    |> Option.defaultWith (fun () -> failwith $"Failed to parse proxy address \"{v}\" of user \"{adUser.DistinguishedName}\"")
                )
            UserPrincipalName = SearchResultEntry.getStringAttributeValue "userPrincipalName" adUser
        }

    let getADOperations (modification: DirectoryModification) =
        match modification with
        | CreateUser newUser -> createUser newUser
        | UpdateUser (userName, userType, ChangeUserName (newUserName, newFirstName, newLastName, newMailAliasNames)) -> changeUserName userName userType (newUserName, newFirstName, newLastName, newMailAliasNames)
        | UpdateUser (userName, userType, SetSokratesId sokratesId) -> setSokratesId userName userType sokratesId
        | UpdateUser (userName, Student oldClassName, MoveStudentToClass newClassName) -> moveStudentToClass userName oldClassName newClassName
        | UpdateUser (_, Teacher, MoveStudentToClass _) -> failwith "Can't move teacher to student class"
        | DeleteUser (userName, userType) -> deleteUser userName userType
        | CreateGroup userType -> createGroup userType
        | UpdateGroup (Teacher, ChangeGroupName _) -> failwith "Can't rename teacher group"
        | UpdateGroup (Student oldClassName, ChangeGroupName newClassName) -> changeStudentGroupName oldClassName newClassName
        | DeleteGroup userType -> deleteGroup userType

    let applyDirectoryModification modification = async {
        let! result =
            getADOperations modification
            |> List.map (fun v -> async {
                try
                    do! Operation.run config.ConnectionConfig v
                    return Ok $"Successfully applied operation {v}"
                with e -> return Error $"Error while applying operation {v}: {e.Message}"
            })
            |> Async.Sequential
        return result |> Result.sequenceAFull
    }

    member _.ApplyDirectoryModifications (modifications: DirectoryModification list) =
        modifications
        |> List.map (fun modification -> async {
            let! result = applyDirectoryModification modification
            return
                result
                |> Result.mapError (
                    List.map (sprintf "* %s")
                    >> List.append [ $"Error while applying modification {modification}" ]
                )
        })
        |> Async.Sequential
        |> Async.map Array.toList

    member _.GetUsers () = async {
        use connection = Ldap.connect config.ConnectionConfig.Ldap
        let! teachers = Ldap.findGroupMembers connection config.Properties.TeacherGroup

        let! classGroups = async {
            let! groups = Ldap.findFullGroupMembers connection config.Properties.StudentGroup [| "sAMAccountName"; "member" |]
            return
                groups
                |> Seq.map (fun group ->
                    let groupName =
                        group
                        |> SearchResultEntry.getStringAttributeValue "sAMAccountName"
                        |> GroupName
                    let members =
                        group
                        |> SearchResultEntry.getStringAttributeValues "member"
                        |> List.map DistinguishedName
                    (groupName, members)
                )
                |> Seq.toList
        }

        let! adUsers =
            [ config.Properties.TeacherContainer; config.Properties.ClassContainer ]
            |> List.map (fun userContainerPath ->
                Ldap.findDescendantUsers connection userContainerPath userProperties
            )
            |> Async.Sequential
            |> Async.map List.concat
        return
            adUsers
            |> List.choose (fun user ->
                tryGetUserType teachers classGroups (DistinguishedName user.DistinguishedName)
                |> Option.map (fun userType -> getUserFromSearchResult userType user)
            )
    }

    member _.GetComputers () = async {
        use connection = Ldap.connect config.ConnectionConfig.Ldap
        let! adComputers = Ldap.findDescendantComputers connection config.Properties.ComputerContainer [| "dNSHostName" |]
        return
            adComputers
            |> List.map (SearchResultEntry.getStringAttributeValue "dNSHostName")
    }

    static member FromEnvironment () = ADApi(Config.fromEnvironment ())

    // let getUser config userName userType = async {
    //     let groupDn = getGroupPathFromUserType userType
    //     let userDn = (let (UserName userName) = userName in DN.childCN userName groupDn)
    //
    //     use connection = Ldap.connect config.ConnectionConfig.Ldap
    //     let! adUser = Ldap.findObjectByDn connection userDn userProperties
    //     return getUserFromSearchResult userType adUser
    // }
    //
    // let getClassGroups = async {
    //     use connection = Ldap.connect config.ConnectionConfig.Ldap
    //     let! classGroups = Ldap.findFullGroupMembers connection config.Properties.StudentGroup [| "sAMAccountName" |]
    //     return
    //         classGroups
    //         |> List.map (SearchResultEntry.getStringAttributeValue "sAMAccountName" >> GroupName)
    // }
    //
