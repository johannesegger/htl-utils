module Untis.Domain

open System

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
