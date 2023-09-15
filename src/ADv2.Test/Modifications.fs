module AD.Test.Modifications

open AD.Configuration
open AD.Core
open AD.Domain
open AD.Test.Setup
open Expecto
open System

//let private config = Config.fromEnvironment ()

let private randomName prefix =
    sprintf "%s-%O" prefix (Guid.NewGuid())

// let private createUser userType =
//     {
//         Name = UserName (randomName "User" |> fun v -> v.Substring(0, 20))
//         SokratesId = Some (SokratesId "1234")
//         FirstName = "Albert"
//         LastName = "Einstein"
//         Type = userType
//     }
let private mailAliases = [
    { IsPrimary = true; UserName = "Albert.Einstein"; Domain = DefaultDomain }
    { IsPrimary = false; UserName = "Einstein.Albert"; Domain = DefaultDomain }
]
let private password = "!A1b2C3#"

let tests =
    testList "Modifications" [
        testCaseTask "Create user and query" (fun () -> task {
            use adApi = new ADApi(config)
            let user = {
                Name = UserName "MOZA"
                SokratesId = Some (SokratesId "MOZAID")
                FirstName = "Albert"
                LastName = "Mozart"
                Type = Teacher
                MailAliases = [ { IsPrimary = true; UserName = "Albert.Mozart"; Domain = DefaultDomain } ]
                Password = "Test123"
            }
            let! result = adApi.ApplyDirectoryModifications([ CreateUser user ])

            Expect.isOk result $"Error while creating user. Result = %A{result}"

            let! adUser = adApi.GetUser(user.Name, user.Type)
            let actualAdUser = {|
                Name = adUser.Name
                SokratesId = adUser.SokratesId
                FirstName = adUser.FirstName
                LastName = adUser.LastName
                Type = adUser.Type
                Mail = adUser.Mail
                ProxyAddresses = adUser.ProxyAddresses
                UserPrincipalName = adUser.UserPrincipalName
            |}
            let expectedAdUser = {|
                Name = user.Name
                SokratesId = user.SokratesId
                FirstName = user.FirstName
                LastName = user.LastName
                Type = user.Type
                Mail = Some { UserName = "MOZA"; Domain = "htlvb.at" }
                ProxyAddresses = [ { Protocol = { Type = SMTP; IsPrimary = true }; Address = { UserName = "Albert.Mozart"; Domain = "htlvb.at" } } ]
                UserPrincipalName = { UserName = "MOZA"; Domain = "htlvb.at" }
            |}
            Expect.equal actualAdUser expectedAdUser "User doesn't have expected attributes"

            let! uniqueProperties = adApi.GetAllUniqueUserProperties()
            let expectedUniqueProperties = {
                UserNames = [ UserName "MOZA" ]
                MailAddressUserNames = [ "MOZA"; "Albert.Mozart" ]
            }
            Expect.equal uniqueProperties expectedUniqueProperties "Expected user attributes as unique user properties"
        })

        // testCase "Moving user changes user properties" (fun () ->
        //     reader {
        //         let group1Name = randomName "Group"
        //         let group2Name = sprintf "%s-2" group1Name
        //         let einstein = createUser (Student (GroupName group1Name))
        //         do! applyDirectoryModifications [
        //             CreateGroup (Student (GroupName group1Name))
        //             CreateGroup (Student (GroupName group2Name))
        //             CreateUser (einstein, mailAliases, password)
        //             UpdateUser (einstein.Name, einstein.Type, (MoveStudentToClass (GroupName group2Name)))
        //         ]

        //         let! (path, department, homePath, group1Members, group2Members) = reader {
        //             use! adCtx = userRootEntry (Student (GroupName group2Name))
        //             let adUser = user adCtx einstein.Name [| "distinguishedName"; "department"; "homeDirectory" |]
        //             use! adGroup1 = groupPathFromUserType (Student (GroupName group1Name)) |> Reader.bind (adDirectoryEntry [|"member"|])
        //             use! adGroup2 = groupPathFromUserType (Student (GroupName group2Name)) |> Reader.bind (adDirectoryEntry [|"member"|])
        //             return
        //                 adUser.Properties.["distinguishedName"].[0] :?> string,
        //                 adUser.Properties.["department"].[0] :?> string,
        //                 adUser.Properties.["homeDirectory"].[0] :?> string,
        //                 adGroup1.Properties.["member"] |> Seq.cast<string> |> Seq.map DistinguishedName |> Seq.toList,
        //                 adGroup2.Properties.["member"] |> Seq.cast<string> |> Seq.map DistinguishedName |> Seq.toList
        //         }

        //         do! applyDirectoryModifications [
        //             DeleteUser (einstein.Name, (Student (GroupName group2Name)))
        //             DeleteGroup (Student (GroupName group2Name))
        //             DeleteGroup (Student (GroupName group1Name))
        //         ]

        //         Expect.stringContains path group2Name "User should be moved to new group container"
        //         Expect.equal department group2Name "Department should be updated when user is moved"
        //         Expect.stringContains homePath group2Name "Home path should be updated when user is moved"
        //         Expect.all group1Members (fun groupMember -> groupMember <> DistinguishedName path) "User should not be member of old group"
        //         Expect.contains group2Members (DistinguishedName path) "User should be member of new group"
        //     }
        //     |> Reader.run config
        // )

        // testCase "Changing group name changes user properties" (fun () ->
        //     reader {
        //         let groupName = randomName "Group"
        //         let newGroupName = sprintf "%s-new" groupName
        //         let einstein = createUser (Student (GroupName groupName))
        //         do! applyDirectoryModifications [
        //             CreateGroup (Student (GroupName groupName))
        //             CreateUser (einstein, mailAliases, password)
        //             UpdateGroup (Student (GroupName groupName), (ChangeGroupName (GroupName newGroupName)))
        //         ]

        //         let! (department, homePath) = reader {
        //             use! adCtx = userRootEntry (Student (GroupName newGroupName))
        //             let adUser = user adCtx einstein.Name [| "department"; "homeDirectory" |]
        //             return
        //                 adUser.Properties.["department"].[0] :?> string,
        //                 adUser.Properties.["homeDirectory"].[0] :?> string
        //         }

        //         do! applyDirectoryModifications [
        //             DeleteUser (einstein.Name, (Student (GroupName newGroupName)))
        //             DeleteGroup (Student (GroupName newGroupName))
        //         ]

        //         Expect.equal department newGroupName "Department should be updated when group name is changed"
        //         Expect.stringContains homePath newGroupName "Home path should be updated when group name is changed"
        //     }
        //     |> Reader.run config
        // )
    ]
