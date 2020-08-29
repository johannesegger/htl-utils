module Untis.Core

open FSharp.Data
open System
open System.IO
open Untis.Domain

[<Literal>]
let private TimetablePath = __SOURCE_DIRECTORY__ + "/data/GPU001.TXT"
type private Timetable = CsvProvider<TimetablePath, Schema=",Class,Teacher,Subject,Room,Day,Period", Separators="\t">

let private timetable =
    Environment.getEnvVarOrFail "UNTIS_GPU001_FILE_PATH"
    |> File.ReadAllText
    |> Timetable.ParseRows

[<Literal>]
let private TeachingDataPath = __SOURCE_DIRECTORY__ + "/data/GPU002.TXT"
type private TeachingData = CsvProvider<TeachingDataPath, Schema=",,,,Class,Teacher,Subject", Separators="\t">

[<Literal>]
let private RoomsPath = __SOURCE_DIRECTORY__ + "/data/GPU005.TXT"
type private Rooms = CsvProvider<RoomsPath, Schema="ShortName,FullName">

let private rooms =
    Environment.getEnvVarOrFail "UNTIS_GPU005_FILE_PATH"
    |> File.ReadAllText
    |> Rooms.ParseRows

[<Literal>]
let private SubjectsPath = __SOURCE_DIRECTORY__ + "/data/GPU006.TXT"
type private Subjects = CsvProvider<SubjectsPath, Schema="ShortName,FullName">

let private subjects =
    Environment.getEnvVarOrFail "UNTIS_GPU006_FILE_PATH"
    |> File.ReadAllText
    |> Subjects.ParseRows

let private timeFrames =
    Environment.getEnvVarOrFail "UNTIS_TIME_FRAMES"
    |> fun s -> s.Split ';'
    |> Seq.map (fun t ->
        t.Split '-'
        |> Seq.choose (tryDo TimeSpan.TryParse)
        |> Seq.toList
        |> function
        | ``begin`` :: [ ``end`` ] -> { BeginTime = ``begin``; EndTime = ``end`` }
        | _ -> failwithf "Can't parse \"%s\" as time frame" t
    )
    |> Seq.toList

let private tryGetTimeFrameFromPeriodNumber v =
    timeFrames
    |> List.tryItem (v - 1)

let private getSubject shortName =
    subjects
    |> Seq.find (fun r -> CIString r.ShortName = CIString shortName)
    |> fun r -> { Subject.ShortName = r.ShortName; FullName = r.FullName }

let getTeachingData () =
    Environment.getEnvVarOrFail "UNTIS_GPU002_FILE_PATH"
    |> File.ReadAllText
    |> TeachingData.ParseRows
    |> Seq.choose (fun row ->
        if not <| String.IsNullOrEmpty row.Class && not <| String.IsNullOrEmpty row.Teacher && not <| String.IsNullOrEmpty row.Subject then
            if CIString row.Subject = CIString "ord" then
                FormTeacher (SchoolClass row.Class, TeacherShortName row.Teacher)
                |> Some
            else
                NormalTeacher (SchoolClass row.Class, TeacherShortName row.Teacher, getSubject row.Subject)
                |> Some
        elif CIString row.Subject = CIString "spr" then
            timetable
            |> Seq.filter (fun r -> CIString r.Teacher = CIString row.Teacher && CIString r.Subject = CIString "spr")
            |> Seq.tryExactlyOne
            |> Option.map (fun timetableEntry ->
                let room =
                    rooms
                    |> Seq.find (fun r -> CIString r.ShortName = CIString timetableEntry.Room)
                    |> fun r -> { ShortName = r.ShortName; FullName = r.FullName }
                Informant (TeacherShortName row.Teacher, room, WorkingDay.tryFromOrdinal timetableEntry.Day |> Option.get, tryGetTimeFrameFromPeriodNumber timetableEntry.Period |> Option.get)
            )
        elif String.IsNullOrEmpty row.Class then
            Custodian (TeacherShortName row.Teacher, getSubject row.Subject)
            |> Some
        else
            None
    )
    |> Seq.toList
