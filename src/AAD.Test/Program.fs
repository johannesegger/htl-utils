open Expecto

let tests = testList "All" [
    AAD.tests
]

[<EntryPoint>]
let main argv =
    runTestsWithArgs defaultConfig argv tests
