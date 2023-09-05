module Program

open System
open System.Globalization

let getFullTests tests students =
    let studentLookup =
        students
        |> List.map (fun (s: Sokrates.Student, d) -> ((s.SchoolClass.ToLower(), s.LastName.ToLower(), s.FirstName1.ToLower()), (s, d)))
        |> Map.ofList
    tests
    |> List.choose (fun (t: TestData.Test) ->
        let studentId = (t.Student.Class.ToLower(), t.Student.LastName.ToLower(), t.Student.FirstName.ToLower())
        match Map.tryFind studentId studentLookup with
        | Some (student, Ok (studentData: StudentInfo.StudentData)) ->
            let result: Letter.FullTest = {
                Test = t
                Student = {
                    Data = student
                    Address = studentData.Address
                    MailAddress = studentData.MailAddress
                }
            }
            Some result
        | Some (_, Error e) ->
            printWarning $"Student data lookup error: %s{e}"
            None
        | None ->
            printWarning $"Student data not found: %A{t.Student}"
            None
    )

let run tenantId clientId studentsGroupId sokratesReferenceDate testFilePath =
    let students = StudentInfo.getLookup tenantId clientId studentsGroupId sokratesReferenceDate
    let tests = TestData.load testFilePath
    let fullTests = getFullTests tests students
    Letter.generateTeacherLetters fullTests
    Letter.generateStudentLetters fullTests
    ()

[<EntryPoint>]
let main argv =
    match argv with
    | [| tenantId; clientId; studentsGroupId; sokratesReferenceDate; testFilePath |] ->
        let referenceDate = DateTime.Parse(sokratesReferenceDate, CultureInfo.InvariantCulture)
        run tenantId clientId studentsGroupId referenceDate testFilePath
        0
    | _ ->
        printfn $"Usage: dotnet run -- <tenantId> <clientId>"
        -1
