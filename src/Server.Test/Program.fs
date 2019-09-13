open Expecto

let tests = testList "All" [
    AAD.tests
    Untis.tests
    WebUntis.tests
    AADGroups.tests
    Thoth.tests
]

[<EntryPoint>]
let main argv =
    runTestsWithArgs defaultConfig argv tests
