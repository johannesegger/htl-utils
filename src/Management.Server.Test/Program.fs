module Management.Server.Test.Program

open Expecto

let tests =
    testList "All" [
        ADModifications.tests
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args tests
