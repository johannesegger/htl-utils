module AD.Test.Program

open AD.Configuration
open AD.Core
open AD.Domain
open Expecto
open System

let config = Config.fromEnvironment ()

let randomName prefix =
    sprintf "%s-%O" prefix (Guid.NewGuid())

let createUser userType =
    {
        Name = UserName (randomName "User" |> fun v -> v.Substring(0, 20))
        SokratesId = Some (SokratesId "1234")
        FirstName = "Albert"
        LastName = "Einstein"
        Type = userType
    }
let password = "!A1b2C3#"

let tests =
    testList "Modifications" [
        testCase "Moving user changes user properties" (fun () ->
            reader {
                let group1Name = randomName "Group"
                let group2Name = sprintf "%s-2" group1Name
                let einstein = createUser (Student (GroupName group1Name))
                do! applyDirectoryModifications [
                    CreateGroup (Student (GroupName group1Name))
                    CreateGroup (Student (GroupName group2Name))
                    CreateUser (einstein, password)
                    UpdateUser (einstein.Name, einstein.Type, (MoveStudentToClass (GroupName group2Name)))
                ]

                let! (path, department, homePath, group1Members, group2Members) = reader {
                    use! adCtx = userRootEntry (Student (GroupName group2Name))
                    let adUser = user adCtx einstein.Name [| "distinguishedName"; "department"; "homeDirectory" |]
                    use! adGroup1 = groupPathFromUserType (Student (GroupName group1Name)) |> Reader.bind (adDirectoryEntry [|"member"|])
                    use! adGroup2 = groupPathFromUserType (Student (GroupName group2Name)) |> Reader.bind (adDirectoryEntry [|"member"|])
                    return
                        adUser.Properties.["distinguishedName"].[0] :?> string,
                        adUser.Properties.["department"].[0] :?> string,
                        adUser.Properties.["homeDirectory"].[0] :?> string,
                        adGroup1.Properties.["member"] |> Seq.cast<string> |> Seq.map DistinguishedName |> Seq.toList,
                        adGroup2.Properties.["member"] |> Seq.cast<string> |> Seq.map DistinguishedName |> Seq.toList
                }

                do! applyDirectoryModifications [
                    DeleteUser (einstein.Name, (Student (GroupName group2Name)))
                    DeleteGroup (Student (GroupName group2Name))
                    DeleteGroup (Student (GroupName group1Name))
                ]

                Expect.stringContains path group2Name "User should be moved to new group container"
                Expect.equal department group2Name "Department should be updated when user is moved"
                Expect.stringContains homePath group2Name "Home path should be updated when user is moved"
                Expect.all group1Members (fun groupMember -> groupMember <> DistinguishedName path) "User should not be member of old group"
                Expect.contains group2Members (DistinguishedName path) "User should be member of new group"
            }
            |> Reader.run config
        )

        testCase "Changing group name changes user properties" (fun () ->
            reader {
                let groupName = randomName "Group"
                let newGroupName = sprintf "%s-new" groupName
                let einstein = createUser (Student (GroupName groupName))
                do! applyDirectoryModifications [
                    CreateGroup (Student (GroupName groupName))
                    CreateUser (einstein, password)
                    UpdateGroup (Student (GroupName groupName), (ChangeGroupName (GroupName newGroupName)))
                ]

                let! (department, homePath) = reader {
                    use! adCtx = userRootEntry (Student (GroupName newGroupName))
                    let adUser = user adCtx einstein.Name [| "department"; "homeDirectory" |]
                    return
                        adUser.Properties.["department"].[0] :?> string,
                        adUser.Properties.["homeDirectory"].[0] :?> string
                }

                do! applyDirectoryModifications [
                    DeleteUser (einstein.Name, (Student (GroupName newGroupName)))
                    DeleteGroup (Student (GroupName newGroupName))
                ]

                Expect.equal department newGroupName "Department should be updated when group name is changed"
                Expect.stringContains homePath newGroupName "Home path should be updated when group name is changed"
            }
            |> Reader.run config
        )
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args tests
