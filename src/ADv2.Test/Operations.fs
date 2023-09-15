module AD.Test.Operations

open AD
open AD.Configuration
open AD.Directory
open AD.Ldap
open AD.Operations
open AD.Test.Setup
open Expecto
open System
open System.IO

type TemporaryFolder() =
    let path = Path.Combine(networkShare, Guid.NewGuid().ToString())
    do
        use _ = NetworkConnection.create connectionConfig.NetworkShare path
        Directory.CreateDirectory(path) |> ignore
    member _.Path = path
    interface IDisposable with
        member self.Dispose() =
            use _ = NetworkConnection.create connectionConfig.NetworkShare path
            Directory.delete self.Path

let private userPassword = "Test123"
let private createUser connection userDn properties = async {
    return! createNodeAndParents connection userDn ADUser [
        yield! properties
        yield! DN.tryCN userDn |> Option.map (fun userName -> "sAMAccountName", Text userName) |> Option.toList
        ("userAccountControl", Text $"{UserAccountControl.NORMAL_ACCOUNT ||| UserAccountControl.DONT_EXPIRE_PASSWORD ||| UserAccountControl.PASSWD_NOTREQD}")
        ("unicodePwd", Bytes (AD.password userPassword))
    ]
}

let tests =
    testList "Operations" [
        testCaseTask "Create group home path" (fun () -> task {
            use folder = new TemporaryFolder()
            let groupFolderPath = Path.Combine(folder.Path, "1AHWIM")

            do! Operation.run connectionConfig (CreateGroupHomePath groupFolderPath)

            Expect.isTrue (Directory.Exists groupFolderPath) "Group folder path should exist"
        })

        testCaseTask "Create user home path" (fun () -> task {
            use folder = new TemporaryFolder()
            let homePath = Path.Combine(folder.Path, "BOHN")
            use connection = Ldap.connect connectionConfig.Ldap
            let user1Dn = DistinguishedName "CN=BOHN1,CN=Users,DC=htlvb,DC=intern"
            use! __ = createUser connection user1Dn [ ("homeDirectory", Text homePath) ]

            do! Operation.run connectionConfig (CreateUserHomePath user1Dn)

            let! selfWriteResult =
                async {
                    use _ = NetworkConnection.create { UserName = "htlvb.intern\\BOHN1"; Password = userPassword } homePath
                    File.WriteAllText(Path.Combine(homePath, "sample.txt"), "Sample text")
                }
                |> Async.Catch

            let user2Dn = DistinguishedName "CN=BOHN2,CN=Users,DC=htlvb,DC=intern"
            use! __ = createUser connection user2Dn []
            let! otherWriteResult =
                async {
                    use _ = NetworkConnection.create { UserName = "htlvb.intern\\BOHN2"; Password = userPassword } homePath
                    File.WriteAllText(Path.Combine(homePath, "sample.txt"), "Sample text")
                }
                |> Async.Catch

            Expect.isChoice1Of2 selfWriteResult "User should be able to write in own home folder"
            Expect.isChoice2Of2 otherWriteResult "Other user should not be able to write in user home folder"
        })
    ]
