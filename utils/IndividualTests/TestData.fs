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
    | ExactTimeSpan of start: TimeSpan * ``end``: TimeSpan * room: string
    | ExactTime of TimeSpan * room: string
    | StartTime of TimeSpan * room: string
    | Afterwards of room: string
    | NoTime
module TestPart =
    let private tryParseTimeSpan (text: string) =
        match DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None) with
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
    let tryGetStartTime = function
        | ExactTimeSpan (start, _, _) -> Some start
        | ExactTime (start, _) -> Some start
        | StartTime (start, _) -> Some start
        | Afterwards _ -> None
        | NoTime -> None
    // let toString = function
    //     | ExactTimeSpan (start, ``end``, room) -> sprintf "%s - %s (%s)" (start.ToString("hh\\:mm")) (``end``.ToString("hh\\:mm")) room
    //     | ExactTime (v, room) -> sprintf "%s (%s)" (v.ToString("hh\\:mm")) room
    //     | StartTime (v, room) -> sprintf "ab %s (%s)" (v.ToString("hh\\:mm")) room
    //     | Afterwards room -> sprintf "anschließend (%s)" room
    //     | NoTime -> "-"
    let toString = function
        | ExactTimeSpan (start, ``end``, room) -> sprintf "%s - %s" (start.ToString("hh\\:mm")) (``end``.ToString("hh\\:mm"))
        | ExactTime (v, room) -> sprintf "%s" (v.ToString("hh\\:mm"))
        | StartTime (v, room) -> sprintf "ab %s" (v.ToString("hh\\:mm"))
        | Afterwards room -> sprintf "anschließend"
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

let private correctDate (date: DateTime) =
    if date.Year <> DateTime.Today.Year then DateTime(DateTime.Today.Year, date.Month, date.Day)
    else date

let private parseTeacher v =
    if String.IsNullOrWhiteSpace v || v = "-" then None
    else Some v

let load (filePath: string) =
    use workbook = new XLWorkbook(filePath)
    let sheet = workbook.Worksheet(1)
    sheet.Rows()
    |> Seq.skip 2
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
                    FirstName = row.Cell("D").GetValue<string>().Trim()
                }
                |> correctStudentData
            Subject = row.Cell("E").GetValue<string>()
            Teacher1 = row.Cell("G").GetValue<string>()
            Teacher2 = row.Cell("H").GetValue<string>() |> parseTeacher
            Date = row.Cell("J").GetValue<string>() |> (fun v -> Date.tryParse v |> Option.defaultWith (fun () -> failwithf "Can't parse \"%s\" as date (row #%d)" v (row.RowNumber()))) |> correctDate
            PartWritten = (row.Cell("K").GetValue<string>(), row.Cell("L").GetValue<string>(), row.Cell("P").GetValue<string>()) |> (fun v -> uncurry3 TestPart.tryParse v |> Option.defaultWith (fun () -> failwithf "Can't parse \"%A\" as test part (row #%d)" v (row.RowNumber())))
            PartOral = (row.Cell("N").GetValue<string>(), row.Cell("O").GetValue<string>(), row.Cell("Q").GetValue<string>()) |> (fun v -> uncurry3 TestPart.tryParse v |> Option.defaultWith (fun () -> failwithf "Can't parse \"%A\" as test part (row #%d)" v (row.RowNumber())))
        }
    )
    |> Seq.toList
