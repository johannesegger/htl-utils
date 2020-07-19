module Untis.DataTransferTypes

open System
open Thoth.Json.Net

type SchoolClass = SchoolClass of string
module SchoolClass =
    let encode (SchoolClass v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map SchoolClass

type TeacherShortName = TeacherShortName of string
module TeacherShortName =
    let encode (TeacherShortName v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map TeacherShortName

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

type Room = {
    ShortName: string
    FullName: string
}
module Room =
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

type WorkingDay = Monday | Tuesday | Wednesday | Thursday | Friday
module WorkingDay =
    open Microsoft.FSharp.Reflection

    let encode v = Encode.string (sprintf "%O" v)
    let decoder : Decoder<WorkingDay> =
        Decode.string
        |> Decode.andThen (fun s ->
            FSharpType.GetUnionCases typeof<WorkingDay>
            |> Seq.filter (fun info -> info.Name = s)
            |> Seq.tryExactlyOne
            |> function
            | Some v -> Decode.succeed (FSharpValue.MakeUnion (v, [||]) :?> WorkingDay)
            | None -> Decode.fail (sprintf "Can't parse \"%s\" as working day" s)
        )
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
module TimeFrame =
    let encode v = Encode.object [
        "beginTime", Encode.timespan v.BeginTime
        "endTime", Encode.timespan v.EndTime
    ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                BeginTime = get.Required.Field "beginTime" Decode.timespan
                EndTime = get.Required.Field "endTime" Decode.timespan
            }
        )

type TeacherTask =
    | NormalTeacher of SchoolClass * TeacherShortName * Subject
    | FormTeacher of SchoolClass * TeacherShortName
    | Custodian of TeacherShortName * Subject
    | Informant of TeacherShortName * Room * WorkingDay * TimeFrame

module TeacherTask =
    let encode = function
        | NormalTeacher (schoolClass, teacher, subject) ->
            let fields = [
                "schoolClass", SchoolClass.encode schoolClass
                "teacher", TeacherShortName.encode teacher
                "subject", Subject.encode subject
            ]
            Encode.object [ "normalTeacher", Encode.object fields ]
        | FormTeacher (schoolClass, teacher) ->
            let fields = [
                "schoolClass", SchoolClass.encode schoolClass
                "teacher", TeacherShortName.encode teacher
            ]
            Encode.object [ "formTeacher", Encode.object fields ]
        | Custodian (teacher, subject) ->
            let fields = [
                "teacher", TeacherShortName.encode teacher
                "subject", Subject.encode subject
            ]
            Encode.object [ "custodian", Encode.object fields ]
        | Informant (teacher, room, workingDay, timeFrame) ->
            let fields = [
                "teacher", TeacherShortName.encode teacher
                "room", Room.encode room
                "workingDay", WorkingDay.encode workingDay
                "timeFrame", TimeFrame.encode timeFrame
            ]
            Encode.object [ "informant", Encode.object fields ]

    let decoder : Decoder<_> =
        let normalTeacherDecoder : Decoder<_> =
            Decode.object (fun get ->
                let schoolClass = get.Required.Field "schoolClass" SchoolClass.decoder
                let teacher = get.Required.Field "teacher" TeacherShortName.decoder
                let subject = get.Required.Field "subject" Subject.decoder
                NormalTeacher (schoolClass, teacher, subject)
            )
        let formTeacherDecoder : Decoder<_> =
            Decode.object (fun get ->
                let schoolClass = get.Required.Field "schoolClass" SchoolClass.decoder
                let teacher = get.Required.Field "teacher" TeacherShortName.decoder
                FormTeacher (schoolClass, teacher)
            )
        let custodianDecoder : Decoder<_> =
            Decode.object (fun get ->
                let teacher = get.Required.Field "teacher" TeacherShortName.decoder
                let subject = get.Required.Field "subject" Subject.decoder
                Custodian (teacher, subject)
            )
        let informantDecoder : Decoder<_> =
            Decode.object (fun get ->
                let teacher = get.Required.Field "teacher" TeacherShortName.decoder
                let room = get.Required.Field "room" Room.decoder
                let workingDay = get.Required.Field "workingDay" WorkingDay.decoder
                let timeFrame = get.Required.Field "timeFrame" TimeFrame.decoder
                Informant (teacher, room, workingDay, timeFrame)
            )
        Decode.oneOf [
            Decode.field "normalTeacher" normalTeacherDecoder
            Decode.field "formTeacher" formTeacherDecoder
            Decode.field "custodian" custodianDecoder
            Decode.field "informant" informantDecoder
        ]
