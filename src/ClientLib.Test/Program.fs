open Expecto
open FsCheck
open ClientLib.Test

[<EntryPoint>]
let main args =
    testList "all" [
        CorrectionSnapshot.tests
        Correction.tests
    ]
    |> runTestsWithArgs defaultConfig args