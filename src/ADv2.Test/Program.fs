module AD.Test.Program

open Expecto

let tests =
    testList "All" [
        DN.tests
        Ldap.tests
        Operations.tests
        Modifications.tests
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args tests
