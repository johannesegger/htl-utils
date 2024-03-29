﻿module Sokrates.Test.Program

open Expecto
open Sokrates

let private sokratesConfig = Config.fromEnvironment ()
let private sokratesApi = SokratesApi(sokratesConfig)

let tests =
    testList "Fetch students" [
        ptestCaseAsync "Should not find inactive teachers" <| async {
            let! teachers = sokratesApi.FetchTeachers
            let lacs =
                teachers
                |> List.tryFind (fun v -> v.ShortName = "LACS")
            Expect.isNone lacs "Should not find LACS"
        }

        ptestCaseAsync "Should not find multiple teachers with same short name" <| async {
            let! teachers = sokratesApi.FetchTeachers
            let duplicateEntries =
                teachers
                |> List.groupBy (fun v -> v.ShortName)
                |> List.filter (fun (_, v) -> v.Length > 1)
                |> List.map snd
            Expect.isEmpty duplicateEntries "Should not have multiple teachers with same short name"
        }

        testCaseAsync "Can fetch student gender" <| async {
            let! students = sokratesApi.FetchStudents None None
            students
            |> List.iter (fun v -> printfn $"%s{v.LastName} %s{v.FirstName1} (%s{v.SchoolClass}): %O{v.Gender}")
        }

        testCaseAsync "Can fetch student contact infos" <| async {
            let! contactInfos = sokratesApi.FetchStudentContactInfos [ SokratesId "41742720230215" ] None
            // TODO contacts where "Entscheide" checkbox is not checked are not shown
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
