module AD.Test.Program

open AD.Core
open AD.Domain
open Expecto
open System

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
        testCase "Change group name" <| fun () ->
            let groupName = randomName "Group"
            let newGroupName = sprintf "%s-new" groupName
            let einstein = createUser (Student (GroupName groupName))
            applyDirectoryModifications [
                CreateGroup (Student (GroupName groupName))
                CreateUser (einstein, password)
                UpdateGroup (Student (GroupName groupName), (ChangeGroupName (GroupName newGroupName)))
            ]

            let department =
                use adCtx = userRootEntry (Student (GroupName newGroupName))
                let adUser = user adCtx einstein.Name [| "department" |]
                adUser.Properties.["department"].[0] :?> string

            applyDirectoryModifications [
                DeleteUser (einstein.Name, (Student (GroupName newGroupName)))
                DeleteGroup (Student (GroupName newGroupName))
            ]

            Expect.equal department newGroupName "Department should be updated when group name is changed"
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args tests
