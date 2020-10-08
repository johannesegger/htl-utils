module Untis.Core

open FSharp.Data
open System
open System.IO
open Untis.Configuration
open Untis.Domain

[<Literal>]
let private TimetablePath = __SOURCE_DIRECTORY__ + "/data/GPU001.TXT"
type private Timetable = CsvProvider<TimetablePath, Schema=",Class,Teacher,Subject,Room,Day,Period">

let private timetable = reader {
    let! config = Reader.environment
    return
        config.GPU001TimetableFilePath
        |> File.ReadAllText
        |> Timetable.ParseRows
}

[<Literal>]
let private TeachingDataPath = __SOURCE_DIRECTORY__ + "/data/GPU002.TXT"
type private TeachingData = CsvProvider<TeachingDataPath, Schema=",,,,Class,Teacher,Subject">

let private teachingData = reader {
    let! config = Reader.environment
    return
        config.GPU002TeachingDataFilePath
        |> File.ReadAllText
        |> TeachingData.ParseRows
}

[<Literal>]
let private RoomsPath = __SOURCE_DIRECTORY__ + "/data/GPU005.TXT"
type private Rooms = CsvProvider<RoomsPath, Schema="ShortName,FullName">

let private rooms = reader {
    let! config = Reader.environment
    return
        config.GPU005RoomsFilePath
        |> File.ReadAllText
        |> Rooms.ParseRows
}

[<Literal>]
let private SubjectsPath = __SOURCE_DIRECTORY__ + "/data/GPU006.TXT"
type private Subjects = CsvProvider<SubjectsPath, Schema="ShortName,FullName">

let private subjects = reader {
    let! config = Reader.environment
    return
        config.GPU006SubjectsFilePath
        |> File.ReadAllText
        |> Subjects.ParseRows
}

let getTeachingData = reader {
    let! teachingData = teachingData
    let! subjects = subjects
    let subjectMap =
        subjects
        |> Seq.map (fun r -> CIString r.ShortName, { Subject.ShortName = r.ShortName; FullName = r.FullName })
        |> Map.ofSeq
    let! timetable = timetable
    let! rooms = rooms
    let! config = Reader.environment
    let tryGetTimeFrameFromPeriodNumber v = config.TimeFrames |> List.tryItem (v - 1)
    return
        teachingData
        |> Seq.choose (fun row ->
            if not <| String.IsNullOrEmpty row.Class && not <| String.IsNullOrEmpty row.Teacher && not <| String.IsNullOrEmpty row.Subject then
                if CIString row.Subject = CIString "ord" then
                    FormTeacher (SchoolClass row.Class, TeacherShortName row.Teacher)
                    |> Some
                else
                    NormalTeacher (SchoolClass row.Class, TeacherShortName row.Teacher, Map.find (CIString row.Subject) subjectMap)
                    |> Some
            elif CIString row.Subject = CIString "spr" then
                timetable
                |> Seq.filter (fun r -> CIString r.Teacher = CIString row.Teacher && CIString r.Subject = CIString "spr")
                |> Seq.tryExactlyOne
                |> Option.map (fun timetableEntry ->
                    let room =
                        rooms
                        |> Seq.tryFind (fun r -> CIString r.ShortName = CIString timetableEntry.Room)
                        |> function
                        | Some r -> { ShortName = r.ShortName; FullName = Some r.FullName }
                        | None -> { ShortName = timetableEntry.Room; FullName = None }
                    Informant (TeacherShortName row.Teacher, room, WorkingDay.tryFromOrdinal timetableEntry.Day |> Option.get, tryGetTimeFrameFromPeriodNumber timetableEntry.Period |> Option.get)
                )
            elif String.IsNullOrEmpty row.Class then
                Custodian (TeacherShortName row.Teacher, Map.find (CIString row.Subject) subjectMap)
                |> Some
            else
                None
        )
        |> Seq.toList
}
