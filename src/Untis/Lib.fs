module Untis

open FSharp.Data
open System
open System.IO

type SchoolClass = SchoolClass of string

type TeacherShortName = TeacherShortName of string

type Subject = {
    ShortName: string
    FullName: string
}

type Room = {
    ShortName: string
    FullName: string option
}

type WorkingDay = Monday | Tuesday | Wednesday | Thursday | Friday
module WorkingDay =
    open Microsoft.FSharp.Reflection
    let tryFromOrdinal v =
        FSharpType.GetUnionCases typeof<WorkingDay>
        |> Array.tryItem (v - 1)
        |> Option.map (fun v -> FSharpValue.MakeUnion (v, [||]) :?> WorkingDay)
    let toGermanString = function
        | Monday -> "Montag"
        | Tuesday -> "Dienstag"
        | Wednesday -> "Mittwoch"
        | Thursday -> "Donnerstag"
        | Friday -> "Freitag"

type TimeFrame = { BeginTime: TimeSpan; EndTime: TimeSpan }

type TeacherTask =
    | NormalTeacher of SchoolClass * TeacherShortName * Subject
    | FormTeacher of SchoolClass * TeacherShortName
    | Custodian of TeacherShortName * Subject
    | Informant of TeacherShortName * Room * WorkingDay * TimeFrame

type Config = {
    GPU001TimetableFilePath: string
    GPU002TeachingDataFilePath: string
    GPU005RoomsFilePath: string
    GPU006SubjectsFilePath: string
    TimeFrames: TimeFrame list
}
module Config =
    open Microsoft.Extensions.Configuration

    type UntisConfig() =
        member val GPU001TimetableFilePath = "" with get, set
        member val GPU002TeachingDataFilePath = "" with get, set
        member val GPU005RoomsFilePath = "" with get, set
        member val GPU006SubjectsFilePath = "" with get, set
        member val TimeFrames = "" with get, set
        member x.Build() = {
            GPU001TimetableFilePath = x.GPU001TimetableFilePath
            GPU002TeachingDataFilePath = x.GPU002TeachingDataFilePath
            GPU005RoomsFilePath = x.GPU005RoomsFilePath
            GPU006SubjectsFilePath = x.GPU006SubjectsFilePath
            TimeFrames =
                x.TimeFrames
                |> String.split ";"
                |> Seq.map (fun t ->
                    String.split "-" t
                    |> Seq.choose (tryDo TimeSpan.TryParse)
                    |> Seq.toList
                    |> function
                    | ``begin`` :: [ ``end`` ] ->
                        let timeFrame = { BeginTime = ``begin``; EndTime = ``end`` }
                        timeFrame
                    | _ -> failwithf "Can't parse \"%s\" as time frame" t
                )
                |> Seq.toList
        }

    let fromEnvironment () =
        let config = ConfigurationBuilder().AddEnvironmentVariables().Build()
        ConfigurationBinder.Get<UntisConfig>(config.GetSection("Untis")).Build()

[<Literal>]
let private TimetablePath = __SOURCE_DIRECTORY__ + "/data/GPU001.TXT"
type private Timetable = CsvProvider<TimetablePath, Schema=",Class,Teacher,Subject,Room,Day,Period">

[<Literal>]
let private TeachingDataPath = __SOURCE_DIRECTORY__ + "/data/GPU002.TXT"
type private TeachingData = CsvProvider<TeachingDataPath, Schema=",,,,Class,Teacher,Subject">

[<Literal>]
let private RoomsPath = __SOURCE_DIRECTORY__ + "/data/GPU005.TXT"
type private Rooms = CsvProvider<RoomsPath, Schema="ShortName,FullName">

[<Literal>]
let private SubjectsPath = __SOURCE_DIRECTORY__ + "/data/GPU006.TXT"
type private Subjects = CsvProvider<SubjectsPath, Schema="ShortName,FullName">

type UntisExport(config) =
    let getTimetable () =
        config.GPU001TimetableFilePath
        |> File.ReadAllText
        |> Timetable.ParseRows

    let getTeachingData () =
        config.GPU002TeachingDataFilePath
        |> File.ReadAllText
        |> TeachingData.ParseRows

    let getRooms () =
        config.GPU005RoomsFilePath
        |> File.ReadAllText
        |> Rooms.ParseRows

    let getSubjects () =
        config.GPU006SubjectsFilePath
        |> File.ReadAllText
        |> Subjects.ParseRows

    member _.GetTeachingData () =
        let teachingData = getTeachingData()
        let subjects = getSubjects()
        let subjectMap =
            subjects
            |> Seq.map (fun r -> CIString r.ShortName, { Subject.ShortName = r.ShortName; FullName = r.FullName })
            |> Map.ofSeq
        let timetable = getTimetable()
        let rooms = getRooms()
        let tryGetTimeFrameFromPeriodNumber v = config.TimeFrames |> List.tryItem (v - 1)
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
                    let workingDay =
                        WorkingDay.tryFromOrdinal timetableEntry.Day
                        |> Option.defaultWith (fun () -> failwith $"Unknown working day in timetable entry %A{timetableEntry}")
                    let timeFrame =
                        tryGetTimeFrameFromPeriodNumber timetableEntry.Period
                        |> Option.defaultWith (fun () -> failwith $"Unknown time frame \"%d{timetableEntry.Period}\" in timetable entry %A{timetableEntry}. Time frames: %A{config.TimeFrames}")
                    Informant (TeacherShortName row.Teacher, room, workingDay, timeFrame)
                )
            elif String.IsNullOrEmpty row.Class && not <| String.IsNullOrEmpty row.Subject then
                Custodian (TeacherShortName row.Teacher, Map.find (CIString row.Subject) subjectMap)
                |> Some
            else
                None
        )
        |> Seq.toList

    static member FromEnvironment () =
        UntisExport(Config.fromEnvironment ())
