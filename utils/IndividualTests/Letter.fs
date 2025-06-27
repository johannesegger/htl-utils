module Letter

open iText.Html2pdf
open iText.Kernel.Geom
open iText.Kernel.Pdf
open iText.Kernel.Utils
open System
open System.Globalization
open System.IO
open System.Text
open iText.Html2pdf.Resolver.Font

module Date =
    let culture = CultureInfo.GetCultureInfo("de-AT")
    let toString (v: DateTime) = sprintf "%s, %s" (v.ToString("ddd", culture)) (v.ToString("d", culture))

type Student = {
    Data: Sokrates.Student
    Address: Sokrates.Address
    MailAddress: string
}

type FullTest = {
    Test: TestData.Test
    Student: Student
}
module FullTest =
    let toSortable v =
        (
            v.Test.Date,
            (
                TestData.TestPart.tryGetStartTime v.Test.PartWritten
                |> function
                | Some v -> v
                | None -> TimeSpan.MaxValue
            ),
            (
                TestData.TestPart.tryGetStartTime v.Test.PartOral
                |> function
                | Some v -> v
                | None -> TimeSpan.MaxValue
            ),
            v.Student.Data.SchoolClass,
            v.Student.Data.LastName,
            v.Student.Data.FirstName1
        )

let private generateTestRow template additionalProperties test =
    let properties =
        List.append
            [
                ("id", test.Test.Id)
                ("lastName", test.Student.Data.LastName)
                ("firstName", test.Student.Data.FirstName1)
                ("class", test.Student.Data.SchoolClass)
                ("subject", test.Test.Subject)
                ("teacher1", test.Test.Teacher1)
                ("teacher2", test.Test.Teacher2 |> Option.defaultValue "-")
                ("date", Date.toString test.Test.Date)
                ("partWritten", TestData.TestPart.toString test.Test.PartWritten)
                ("partOral", TestData.TestPart.toString test.Test.PartOral)
            ]
            additionalProperties
    (template, properties)
    ||> List.fold (fun s (k, v) -> String.replace (sprintf "{{%s}}" k) v s)

let private generateTeacherLetter letterTemplate testRowTemplate teacherShortName tests =
    tests
    |> List.groupBy (fun test -> test.Test.Date)
    |> List.sortBy fst
    |> List.map (fun (date, tests) ->
        let testTableRows =
            tests
            |> List.sortBy FullTest.toSortable
            |> List.map (fun test ->
                let additionalProperties = [
                    "row-class", (if test.Test.Teacher1 = teacherShortName then "is-pruefer" else "is-beisitz")
                    "pruefer-reference", (if test.Test.Teacher2 = Some teacherShortName then sprintf "s. %s" test.Test.Teacher1 else "&nbsp;")
                ]
                generateTestRow testRowTemplate additionalProperties test
            )
            |> String.concat ""
        letterTemplate
        |> String.replace "{{date}}" (Date.toString date)
        |> String.replace "{{teacherShortName}}" teacherShortName
        |> String.replace "{{testTableRows}}" testTableRows
        |> String.replace "{{result-receiver}}" (if teacherShortName <> "STAL" then "STAL" else "dir selbst")
    )
    |> String.concat ""

let generateTeacherLetters tests includeRoom =
    let documentTemplate = File.ReadAllText @"templates\teacher-letter\document-template.html"
    let letterTemplate = File.ReadAllText @"templates\teacher-letter\letter-template.html"
    let testRowTemplate = File.ReadAllText @"templates\teacher-letter\test-row-template.html"
    let teachers =
        tests
        |> List.collect (fun v -> [ v.Test.Teacher1; yield! Option.toList v.Test.Teacher2 ])
        |> List.distinct
        |> List.sort
    let letters =
        teachers
        |> List.map (fun teacherShortName ->
            let teacherTests =
                tests
                |> List.filter (fun v -> teacherShortName = v.Test.Teacher1 || Some teacherShortName = v.Test.Teacher2)
            let teacherLetter = generateTeacherLetter letterTemplate testRowTemplate teacherShortName teacherTests
            (teacherShortName, teacherLetter)
        )

    let targetDir = @"out\teachers"
    Directory.CreateDirectory(targetDir) |> ignore

    let includeRoomStyle =
        if includeRoom then ".no-include-room { display: none; }"
        else ".include-room { display: none; }"
    letters
    |> List.iter (fun (teacherShortName, content) ->
        let document =
            documentTemplate
            |> String.replace "{{header}}" $"<style>%s{includeRoomStyle}</style>"
            |> String.replace "{{teacherShortName}}" teacherShortName
            |> String.replace "{{content}}" content
        File.WriteAllText(Path.Combine(targetDir, sprintf "%s.html" teacherShortName), document)
        
    )

let getSalutation = function
    | Sokrates.Male -> "Herr"
    | Sokrates.Female -> "Frau"

let generateStudentLetter letterTemplate testRowTemplate student tests =
    let testTableRows =
        tests
        |> List.sortBy FullTest.toSortable
        |> List.map (generateTestRow testRowTemplate [])
        |> String.concat ""
    let testType = tests.Head.Test.TestType
    letterTemplate
    |> String.replace "{{salutation}}" (getSalutation student.Data.Gender)
    |> String.replace "{{name}}" $"%s{student.Data.LastName} %s{student.Data.FirstName1}"
    |> String.replace "{{street}}" student.Address.Street
    |> String.replace "{{zipCode}}" student.Address.Zip
    |> String.replace "{{city}}" student.Address.City
    |> String.replace "{{testTableRows}}" testTableRows
    |> String.replace "{{testCountGroup}}" (if tests.Length = 1 then "single-test" else "multiple-tests")
    |> String.replace "{{date}}" (DateTime.Today.ToString("D", CultureInfo.GetCultureInfo("de-AT")))
    |> String.replace "{{testTypeSingular}}" (TestData.TestType.singularText testType)
    |> String.replace "{{testTypePlural}}" (TestData.TestType.pluralText testType)

let generateStudentLetters tests includeRoom =
    let documentTemplate = File.ReadAllText @"templates\student-letter\document-template.html"
    let letterTemplate = File.ReadAllText @"templates\student-letter\letter-template.html"
    let testRowTemplate = File.ReadAllText @"templates\student-letter\test-row-template.html"

    let targetDir = @"out\students"
    Directory.CreateDirectory(targetDir) |> ignore

    let students =
        tests
        |> List.map (fun v -> v.Student)
        |> List.distinct
        |> List.sortBy (fun v -> (v.Data.SchoolClass, v.Data.LastName, v.Data.FirstName1))
    let letters =
        students
        |> List.map (fun student ->
            let studentTests =
                tests
                |> List.filter (fun v -> v.Student = student)
            let studentLetter = generateStudentLetter letterTemplate testRowTemplate student studentTests
            (student, studentLetter)
        )

    let includeRoomStyle =
        if includeRoom then ".no-include-room { display: none; }"
        else ".include-room { display: none; }"
    letters
    |> List.iter (fun (student, content) ->
        let document =
            documentTemplate
            |> String.replace "{{header}}" $"<style>%s{includeRoomStyle}</style>"
            |> String.replace "{{content}}" content
        let (Sokrates.SokratesId sokratesId) = student.Data.Id
        File.WriteAllText(Path.Combine(targetDir, sprintf "%s %s - %s.html" student.Data.LastName student.Data.FirstName1 sokratesId), document)
    )