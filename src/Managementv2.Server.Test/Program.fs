module Managementv2.Server.Test.Program

open Expecto

[<Tests>]
let tests =
    testList "All" [
        CustomOperationsConfig.tests
        CustomOperationsStore.tests
    ]

[<EntryPoint>]
let main args = runTestsWithCLIArgs [] args tests