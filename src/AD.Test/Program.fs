module AD.Test.Program

open AD
open Expecto
open System

let private adConfig = Config.fromEnvironment ()
let private adHelper = ADHelper(adConfig)
let private adApi = ADApi(adConfig)

let private randomName prefix =
    sprintf "%s-%O" prefix (Guid.NewGuid())

let private createUser userType =
    {
        Name = UserName (randomName "User" |> fun v -> v.Substring(0, 20))
        SokratesId = Some (SokratesId "1234")
        FirstName = "Albert"
        LastName = "Einstein"
        Type = userType
    }
let private mailAliases = [
    { IsPrimary = true; UserName = "Albert.Einstein" }
    { IsPrimary = false; UserName = "Einstein.Albert" }
]
let private password = "!A1b2C3#"

let tests =
    testList "Modifications" [
        testCase "Can create group" (fun () ->
            let userType = randomName "Group" |> GroupName |> Student
            adApi.ApplyDirectoryModifications [
                CreateGroup userType
            ]
            let (ouChildren, groupMembers) =
                use adCtx = adHelper.FetchUserOu userType
                use adGroup = adHelper.GetGroupPathFromUserType userType |> adHelper.FetchDirectoryEntry [|"member"|]
                adCtx.Children |> Seq.cast<obj> |> Seq.toList,
                adGroup.Properties.["member"] |> Seq.cast<string> |> Seq.map DistinguishedName |> Seq.toList
            adApi.ApplyDirectoryModifications [
                DeleteGroup userType
            ]

            Expect.isEmpty ouChildren "OU should be empty"
            Expect.isEmpty groupMembers "Group should not have members"
        )

        testCase "Can create user" (fun () ->
            let userType = randomName "Group" |> GroupName |> Student
            let user = createUser userType
            adApi.ApplyDirectoryModifications [
                CreateGroup userType
                CreateUser (user, mailAliases, password)
            ]
            adApi.ApplyDirectoryModifications [
                DeleteUser (user.Name, userType)
                DeleteGroup userType
            ]
        )

        testCase "Moving user changes user properties" (fun () ->
            let group1Name = randomName "Group"
            let group2Name = sprintf "%s-2" group1Name
            let einstein = createUser (Student (GroupName group1Name))
            adApi.ApplyDirectoryModifications [
                CreateGroup (Student (GroupName group1Name))
                CreateGroup (Student (GroupName group2Name))
                CreateUser (einstein, mailAliases, password)
                UpdateUser (einstein.Name, einstein.Type, (MoveStudentToClass (GroupName group2Name)))
            ]

            let (path, department, homePath, group1Members, group2Members) =
                use adCtx = adHelper.FetchUserOu (Student (GroupName group2Name))
                let adUser = adHelper.FindUser adCtx einstein.Name [| "distinguishedName"; "department"; "homeDirectory" |]
                use adGroup1 = adHelper.GetGroupPathFromUserType (Student (GroupName group1Name)) |> adHelper.FetchDirectoryEntry [|"member"|]
                use adGroup2 = adHelper.GetGroupPathFromUserType (Student (GroupName group2Name)) |> adHelper.FetchDirectoryEntry [|"member"|]
                adUser.Properties.["distinguishedName"].[0] :?> string,
                adUser.Properties.["department"].[0] :?> string,
                adUser.Properties.["homeDirectory"].[0] :?> string,
                adGroup1.Properties.["member"] |> Seq.cast<string> |> Seq.map DistinguishedName |> Seq.toList,
                adGroup2.Properties.["member"] |> Seq.cast<string> |> Seq.map DistinguishedName |> Seq.toList

            adApi.ApplyDirectoryModifications [
                DeleteUser (einstein.Name, (Student (GroupName group2Name)))
                DeleteGroup (Student (GroupName group2Name))
                DeleteGroup (Student (GroupName group1Name))
            ]

            Expect.stringContains path group2Name "User should be moved to new group container"
            Expect.equal department group2Name "Department should be updated when user is moved"
            Expect.stringContains homePath group2Name "Home path should be updated when user is moved"
            Expect.all group1Members (fun groupMember -> groupMember <> DistinguishedName path) "User should not be member of old group"
            Expect.contains group2Members (DistinguishedName path) "User should be member of new group"
        )

        testCase "Changing group name changes user properties" (fun () ->
            let groupName = randomName "Group"
            let newGroupName = sprintf "%s-new" groupName
            let einstein = createUser (Student (GroupName groupName))
            adApi.ApplyDirectoryModifications [
                CreateGroup (Student (GroupName groupName))
                CreateUser (einstein, mailAliases, password)
                UpdateGroup (Student (GroupName groupName), (ChangeGroupName (GroupName newGroupName)))
            ]

            let (department, homePath) =
                use adCtx = adHelper.FetchUserOu (Student (GroupName newGroupName))
                let adUser = adHelper.FindUser adCtx einstein.Name [| "department"; "homeDirectory" |]
                adUser.Properties.["department"].[0] :?> string,
                adUser.Properties.["homeDirectory"].[0] :?> string

            adApi.ApplyDirectoryModifications [
                DeleteUser (einstein.Name, (Student (GroupName newGroupName)))
                DeleteGroup (Student (GroupName newGroupName))
            ]

            Expect.equal department newGroupName "Department should be updated when group name is changed"
            Expect.stringContains homePath newGroupName "Home path should be updated when group name is changed"
        )
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args tests
