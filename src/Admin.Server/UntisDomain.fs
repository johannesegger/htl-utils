module Untis

open Thoth.Json.Net

type SchoolClass = SchoolClass of string
module SchoolClass =
    let decoder : Decoder<_> = Decode.string |> Decode.map SchoolClass

type TeacherShortName = TeacherShortName of string
module TeacherShortName =
    let decoder : Decoder<_> = Decode.string |> Decode.map TeacherShortName

type Subject = Subject of string
module Subject =
    let decoder : Decoder<_> = Decode.string |> Decode.map Subject

type TeacherTask =
    | NormalTeacher of SchoolClass * TeacherShortName * Subject
    | FormTeacher of SchoolClass * TeacherShortName

module TeacherInClass =
    let private normalTeacherDecoder : Decoder<_> =
        Decode.object (fun get ->
            let schoolClass = get.Required.Field "schoolClass" SchoolClass.decoder
            let teacher = get.Required.Field "teacher" TeacherShortName.decoder
            let subject = get.Required.Field "subject" Subject.decoder
            NormalTeacher (schoolClass, teacher, subject)
        )
    let private formTeacherDecoder : Decoder<_> =
        Decode.object (fun get ->
            let schoolClass = get.Required.Field "schoolClass" SchoolClass.decoder
            let teacher = get.Required.Field "teacher" TeacherShortName.decoder
            FormTeacher (schoolClass, teacher)
        )

    let decoder : Decoder<_> =
        Decode.oneOf [
            Decode.field "normalTeacher" (normalTeacherDecoder |> Decode.map Some)
            Decode.field "formTeacher" (formTeacherDecoder |> Decode.map Some)
            Decode.object (fun get -> None)
        ]
