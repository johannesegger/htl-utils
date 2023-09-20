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

        testCaseAsync "Can fetch student contact infos" <| async {
            let! contactInfos = sokratesApi.FetchStudentContactInfos [ SokratesId "41742720230215" ] None
            contactInfos
            |> List.iter (fun v ->
                printfn $"* {v.StudentId}"
                v.ContactAddresses
                |> List.iter (fun v -> printfn $"  %s{v.Name} %O{v.EMailAddress}")
            )
        }
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args tests
