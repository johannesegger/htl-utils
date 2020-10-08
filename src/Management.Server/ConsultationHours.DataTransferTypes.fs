namespace ConsultationHours.DataTransferTypes

open System
#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

type Teacher =
    {
        ShortName: string
        FirstName: string
        LastName: string
    }

module Teacher =
    let encode t =
        Encode.object [
            "shortName", Encode.string t.ShortName
            "firstName", Encode.string t.FirstName
            "lastName", Encode.string t.LastName
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                ShortName = get.Required.Field "shortName" Decode.string
                FirstName = get.Required.Field "firstName" Decode.string
                LastName = get.Required.Field "lastName" Decode.string
            }
        )

type Subject = {
    ShortName: string
    FullName: string
}
module Subject =
    let encode v = Encode.object [
        "shortName", Encode.string v.ShortName
        "fullName", Encode.string v.FullName
    ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                ShortName = get.Required.Field "shortName" Decode.string
                FullName = get.Required.Field "fullName" Decode.string
            }
        )

type TeacherSubject = {
    Class: string
    Subject: Subject
}

module TeacherSubject =
    let encode t =
        Encode.object [
            "class", Encode.string t.Class
            "subject", Subject.encode t.Subject
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Class = get.Required.Field "class" Decode.string
                Subject = get.Required.Field "subject" Subject.decoder
            }
        )

type Room = {
    ShortName: string
    FullName: string option
}
module Room =
    let encode v = Encode.object [
        "shortName", Encode.string v.ShortName
        "fullName", Encode.option Encode.string v.FullName
    ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                ShortName = get.Required.Field "shortName" Decode.string
                FullName = get.Required.Field "fullName" (Decode.option Decode.string)
            }
        )
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

module ConsultationHourDetails =
    let encode e =
        Encode.object [
            "dayOfWeek", Encode.string e.DayOfWeek
            "beginTime", Encode.timespan e.BeginTime
            "endTime", Encode.timespan e.EndTime
            "location", Room.encode e.Location
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                DayOfWeek = get.Required.Field "dayOfWeek" Decode.string
                BeginTime = get.Required.Field "beginTime" Decode.timespan
                EndTime = get.Required.Field "endTime" Decode.timespan
                Location = get.Required.Field "location" Room.decoder
            }
        )

type ConsultationHourEntry =
    {
        Teacher: Teacher
        Subjects: TeacherSubject list
        FormTeacherOfClasses: string list
        Details: ConsultationHourDetails option
    }

module ConsultationHourEntry =
    let encode e =
        Encode.object [
            "teacher", Teacher.encode e.Teacher
            "subjects", (List.map TeacherSubject.encode >> Encode.list) e.Subjects
            "formTeacherOfClasses", (List.map Encode.string >> Encode.list) e.FormTeacherOfClasses
            "details", Encode.option ConsultationHourDetails.encode e.Details
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Teacher = get.Required.Field "teacher" Teacher.decoder
                Subjects = get.Required.Field "subjects" (Decode.list TeacherSubject.decoder)
                FormTeacherOfClasses = get.Required.Field "formTeacherOfClasses" (Decode.list Decode.string)
                Details = get.Optional.Field "details" ConsultationHourDetails.decoder
            }
        )
