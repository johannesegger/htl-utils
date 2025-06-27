module Program

open System
open System.Globalization
open System.IO

let getFullTests tests students =
    let studentLookup =
        students
        |> List.map (fun (s: Sokrates.Student, d) ->
            ((s.SchoolClass.ToLower(), s.LastName.ToLower(), s.FirstName1.ToLower()), (s, d))
        )
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
            printWarning $"Student data lookup error: %s{TestData.Student.toString t.Student}: %s{e}"
            None
        | None ->
            printWarning $"Student data not found: %s{TestData.Student.toString t.Student}"
            None
    )

let run tenantId clientId studentsGroupId sokratesReferenceDates testFilePath =
    let students = StudentInfo.getLookup tenantId clientId studentsGroupId sokratesReferenceDates
    let tests = TestData.load testFilePath
    match TestData.getProblems tests with
    | [] -> printfn "No problems found"
    | v ->
        printWarning $"%d{v.Length} problems found:"
        v |> List.iter (fun v -> printWarning $"* %s{TestData.Problem.toString v}")
    let fullTests = getFullTests tests students
    Letter.generateTeacherLetters fullTests
    Letter.generateStudentLetters fullTests
    ()

[<EntryPoint>]
let main argv =
    match argv with
    | [| tenantId; clientId; studentsGroupId; sokratesReferenceDates; testFilePath |] ->
        let referenceDates = sokratesReferenceDates.Split(',') |> Seq.map (fun v -> DateTime.Parse(v, CultureInfo.InvariantCulture)) |> Seq.toList
        run tenantId clientId studentsGroupId referenceDates testFilePath
        0
    | _ ->
        printfn $"Usage: dotnet run -- <tenantId> <clientId>"
        -1
