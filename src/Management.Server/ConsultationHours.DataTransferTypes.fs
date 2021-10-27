namespace ConsultationHours.DataTransferTypes

open System
#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

type Teacher = {
    ShortName: string
    FirstName: string
    LastName: string
}

type Subject = {
    ShortName: string
    FullName: string
}

type TeacherSubject = {
    Class: string
    Subject: Subject
}

type Room = {
    ShortName: string
    FullName: string option
}
module Room =
    let toString v =
        match v.FullName with
        | Some fullName -> sprintf "%s - %s" v.ShortName fullName
        | None -> v.ShortName

type ConsultationHourDetails = {
    DayOfWeek: string
    BeginTime: TimeSpan
    EndTime: TimeSpan
    Location: Room
}

type ConsultationHourEntry = {
    Teacher: Teacher
    Subjects: TeacherSubject list
    FormTeacherOfClasses: string list
    Details: ConsultationHourDetails option
}

module Thoth =
    let addCoders (v: ExtraCoders) = v
