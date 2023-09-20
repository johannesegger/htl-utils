module AD.Test.Operations

open AD
open AD.Configuration
open AD.Directory
open AD.Ldap
open AD.Operations
open AD.Test.Setup
open Expecto
open NetworkShare
open System
open System.IO

type TemporaryFolder(networkShare: NetworkShare) =
    let path = Path.Combine(networkSharePath, Guid.NewGuid().ToString())
    do
        networkShare.Open(path)
        Directory.CreateDirectory(path) |> ignore
    member _.Path = path
    interface IDisposable with
        member self.Dispose() =
            networkShare.Open(path)
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
            use networkShare = new NetworkShare(connectionConfig.NetworkShare)
            use folder = new TemporaryFolder(networkShare)
            let groupFolderPath = Path.Combine(folder.Path, "1AHWIM")
            use ldap = new Ldap(connectionConfig.Ldap)

            do! Operation.run ldap networkShare (CreateGroupHomePath groupFolderPath)

            Expect.isTrue (Directory.Exists groupFolderPath) "Group folder path should exist"
        })

        testCaseTask "Create user home path" (fun () -> task {
            use networkShare = new NetworkShare(connectionConfig.NetworkShare)
            use folder = new TemporaryFolder(networkShare)
            let homePath = Path.Combine(folder.Path, "BOHN")
            use ldap = new Ldap(connectionConfig.Ldap)
            let user1Dn = DistinguishedName "CN=BOHN1,CN=Users,DC=htlvb,DC=intern"
            use! __ = createUser ldap user1Dn [ ("homeDirectory", Text homePath) ]

            do! Operation.run ldap networkShare (CreateUserHomePath user1Dn)

            (networkShare :> IDisposable).Dispose()

            let! selfWriteResult =
                async {
                    use networkShare = new NetworkShare({ UserName = "htlvb.intern\\BOHN1"; Password = userPassword })
                    networkShare.Open(homePath)
                    File.WriteAllText(Path.Combine(homePath, "sample.txt"), "Sample text")
                }
                |> Async.Catch

            let user2Dn = DistinguishedName "CN=BOHN2,CN=Users,DC=htlvb,DC=intern"
            use! __ = createUser ldap user2Dn []
            let! otherWriteResult =
                async {
                    use networkShare = new NetworkShare({ UserName = "htlvb.intern\\BOHN2"; Password = userPassword })
                    networkShare.Open(homePath)
                    File.WriteAllText(Path.Combine(homePath, "sample.txt"), "Sample text")
                }
                |> Async.Catch

            Expect.isChoice1Of2 selfWriteResult "User should be able to write in own home folder"
            Expect.isChoice2Of2 otherWriteResult "Other user should not be able to write in user home folder"
        })
    ]
