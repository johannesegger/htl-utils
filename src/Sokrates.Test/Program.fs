module Sokrates.Test.Program

open Expecto
open Sokrates

let private sokratesConfig = Config.fromEnvironment ()
let private sokratesApi = SokratesApi(sokratesConfig)

let tests =
    testList "Fetch students" [
        testCaseAsync "Can fetch student gender" <| async {
            let! students = sokratesApi.FetchStudents None None
            students
            |> List.iter (fun v -> printfn $"%s{v.LastName} %s{v.FirstName1} (%s{v.SchoolClass}): %O{v.Gender}")
        }
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args tests
