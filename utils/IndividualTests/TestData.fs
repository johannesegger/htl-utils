module TestData

open ClosedXML.Excel
open System
open System.Globalization
open System.Text.RegularExpressions

module Date =
    let culture = CultureInfo.GetCultureInfo("de-AT")
    let tryParse (v: string) =
        let rawDate = 
            match v.IndexOf(',') with
            | -1 -> v.Trim()
            | idx -> v.Substring(idx + 1).Trim()
        match DateTime.TryParseExact(rawDate, [| "dd.MM.yyyy"; "d.M.yyyy" |], culture, DateTimeStyles.AllowWhiteSpaces) with
        | (true, d) -> Some d
        | (false, _) -> None

type TestPart =
    | ExactTimeSpan of start: TimeSpan * ``end``: TimeSpan * room: string option
    | ExactTime of TimeSpan * room: string option
    | StartTime of TimeSpan * room: string option
    | Afterwards of room: string option
    | NoTime
module TestPart =
    let private tryParseTimeSpan (text: string) =
        match DateTime.TryParse(text) with
        | (true, v) -> Some v.TimeOfDay
        | _ -> None
    let private tryParseExactTime text room =
        match tryParseTimeSpan text with
        | Some v -> ExactTime (v, room) |> Some
        | _ -> None
    let private tryParseExactTimeSpan start ``end`` room =
        match tryParseTimeSpan start, tryParseTimeSpan ``end`` with
        | Some start, Some ``end`` -> ExactTimeSpan (start, ``end``, room) |> Some
        | _ -> None
    let private tryParseStartTime (text: string) room =
        let m = Regex.Match(text, @"(Ab|ab) (?<time>\d\d:\d\d)")
        if m.Success then StartTime (TimeSpan.Parse(m.Groups.["time"].Value), room) |> Some
        else None
    let private tryParseAfterwards (text: string) room =
        if text = "im Anschluss" then Afterwards room |> Some
        else None
    let private tryParseNoTime (text: string) =
        if text = "" then Some NoTime
        else None
    let tryParse start ``end`` room =
        tryParseExactTimeSpan start ``end`` room
        |> Option.orElse (tryParseExactTime start room)
        |> Option.orElse (tryParseStartTime start room)
        |> Option.orElse (tryParseAfterwards start room)
        |> Option.orElse (tryParseNoTime start)
    let parseRoom v =
        if String.IsNullOrWhiteSpace v then None
        else Some v
    let tryGetStartTime = function
        | ExactTimeSpan (start, _, _) -> Some start
        | ExactTime (start, _) -> Some start
        | StartTime (start, _) -> Some start
        | Afterwards _ -> None
        | NoTime -> None
    let tryGetRoom = function
        | ExactTimeSpan (_, _, room) -> room
        | ExactTime (_, room) -> room
        | StartTime (_, room) -> room
        | Afterwards room -> room
        | NoTime -> None
    let toString = function
        | ExactTimeSpan (start, ``end``, room) ->
            let roomText = room |> Option.map (fun v -> $" (%s{v})") |> Option.defaultValue ""
            sprintf "%s - %s%s" (start.ToString("hh\\:mm")) (``end``.ToString("hh\\:mm")) roomText
        | ExactTime (v, room) ->
            let roomText = room |> Option.map (fun v -> $" (%s{v})") |> Option.defaultValue ""
            sprintf "%s (%s)" (v.ToString("hh\\:mm")) roomText
        | StartTime (v, room) ->
            let roomText = room |> Option.map (fun v -> $" (%s{v})") |> Option.defaultValue ""
            sprintf "ab %s (%s)" (v.ToString("hh\\:mm")) roomText
        | Afterwards room ->
            let roomText = room |> Option.map (fun v -> $" (%s{v})") |> Option.defaultValue ""
            sprintf "anschließend (%s)" roomText
        | NoTime -> "-"

type TestType = Wiederholungspruefung | NOSTPruefung | Uebertrittspruefung | Semesterpruefung
module TestType =
    let singularText = function
        | Wiederholungspruefung -> "Wiederholungsprüfung"
        | NOSTPruefung -> "Semesterprüfung"
        | Uebertrittspruefung -> "Übertrittsprüfung"
        | Semesterpruefung -> "Semesterprüfung"
    let pluralText = function
        | Wiederholungspruefung -> "Wiederholungsprüfungen"
        | NOSTPruefung -> "Semesterprüfungen"
        | Uebertrittspruefung -> "Übertrittsprüfungen"
        | Semesterpruefung -> "Semesterprüfungen"

type Student = {
    Class: string
    FirstName: string
    LastName: string
}
module Student =
    let toString v =
        $"%s{v.LastName} %s{v.FirstName} (%s{v.Class})"

type Test = {
    Id: string
    TestType: TestType
    Student: Student
    Subject: string
    Teacher1: string
    Teacher2: string option
    Date: DateTime
    PartWritten: TestPart
    PartOral: TestPart
}

// TODO modify student in case the data in the excel sheet is not correct
let private correctStudentData (student: Student) = student

let private parseDate v =
    if v |> String.equalsCaseInsensitive "Mo" then DateTime(2024, 09, 09)
    elif v |> String.equalsCaseInsensitive "Di" then DateTime(2024, 09, 10)
    else failwith $"Can't parse date \"%s{v}\""

let private parseTeacher v =
    if String.IsNullOrWhiteSpace v || v = "-" then None
    else Some v

let load (filePath: string) includeRoom =
    use workbook = new XLWorkbook(filePath)
    let sheet = workbook.Worksheet(1)
    sheet.Rows()
    |> Seq.skip 1
    |> Seq.filter (fun row ->
        row.Cell("A").GetValue<string>() |> String.IsNullOrEmpty |> not &&
        row.Cell("B").GetValue<string>() |> String.IsNullOrEmpty |> not
    )
    |> Seq.map (fun row ->
        {
            Id = row.Cell("A").GetValue<string>()
            TestType = Wiederholungspruefung
            Student =
                {
                    Class = row.Cell("B").GetValue<string>().Trim()
                    LastName = row.Cell("C").GetValue<string>().Trim()
                    FirstName = row.Cell("C").GetValue<string>().Trim()
                }
                |> correctStudentData
            Subject = row.Cell("E").GetValue<string>()
            Teacher1 = row.Cell("F").GetValue<string>()
            Teacher2 = row.Cell("G").GetValue<string>() |> parseTeacher
            Date = row.Cell("M").GetValue<string>() |> parseDate
            PartWritten = (row.Cell("N").GetValue<string>(), row.Cell("O").GetValue<string>(), if includeRoom then TestPart.parseRoom (row.Cell("P").GetValue<string>()) else None) |> (fun v -> uncurry3 TestPart.tryParse v |> Option.defaultWith (fun () -> failwithf "Can't parse \"%A\" as test part (row #%d)" v (row.RowNumber())))
            PartOral = (row.Cell("Q").GetValue<string>(), row.Cell("R").GetValue<string>(), if includeRoom then TestPart.parseRoom (row.Cell("S").GetValue<string>()) else None) |> (fun v -> uncurry3 TestPart.tryParse v |> Option.defaultWith (fun () -> failwithf "Can't parse \"%A\" as test part (row #%d)" v (row.RowNumber())))
        }
    )
    |> Seq.toList

type Problem =
    | StudentWithMultipleTestsAtSameDate of Student * DateOnly * subjects: string list
    | TeacherWithMultipleTestsInDifferentRooms of teacher: string * (Test list)
module Problem =
    let toString = function
        | StudentWithMultipleTestsAtSameDate (student, date, subjects) ->
            let subjectsText = String.concat ", " subjects
            $"%s{Student.toString student}: %s{subjectsText} (%A{date})"
        | TeacherWithMultipleTestsInDifferentRooms (teacher, tests) ->
            let testsText =
                tests
                |> List.groupBy (fun v -> TestPart.tryGetRoom v.PartWritten)
                |> List.map (fun (room, tests) ->
                    let roomText = room |> Option.defaultValue "kein Raum"
                    $"%d{tests.Length} x %s{roomText}"
                )
                |> String.concat ", "
            $"%s{teacher}: %s{testsText}"

let getProblems tests =
    let students = tests |> List.map _.Student |> List.distinct |> List.sortBy (fun v -> v.Class, v.LastName, v.FirstName)
    [
        yield! students
        |> List.collect (fun student ->
            let studentTests = 
                tests
                |> List.filter (fun test -> test.Student = student)
            let dates =
                studentTests |> List.map (fun test -> DateOnly.FromDateTime test.Date) |> List.distinct
            dates
            |> List.choose (fun date ->
                match studentTests |> List.filter (fun test -> DateOnly.FromDateTime test.Date = date) with
                | [ _ ] -> None
                | tests -> Some (StudentWithMultipleTestsAtSameDate (student, date, tests |> List.map _.Subject))
            )
        )

        let teachers = tests |> List.collect (fun test -> [ test.Teacher1; yield! Option.toList test.Teacher2 ]) |> List.distinct |> List.sort
        yield! teachers
        |> List.collect (fun teacher ->
            let teacherTests = tests |> List.filter (fun test -> test.Teacher1 = teacher || test.Teacher2 = Some teacher)
            let teacherTestsByDate =
                teacherTests |> List.groupBy (fun test -> DateOnly.FromDateTime test.Date)
            teacherTestsByDate
            |> List.choose (fun (date, teacherTestsAtDate) ->
                if teacherTestsAtDate |> List.choose (fun v -> TestPart.tryGetRoom v.PartWritten) |> List.distinct |> List.length > 1 then
                    Some (TeacherWithMultipleTestsInDifferentRooms (teacher, teacherTestsAtDate))
                else None
            )
        )
    ]
