module Untis.DataTransferTypes

open Thoth.Json.Net

type SchoolClass = SchoolClass of string
module SchoolClass =
    let encode (SchoolClass v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map SchoolClass

type TeacherShortName = TeacherShortName of string
module TeacherShortName =
    let encode (TeacherShortName v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map TeacherShortName

type Subject = Subject of string
module Subject =
    let encode (Subject v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map Subject

type TeacherTask =
    | NormalTeacher of SchoolClass * TeacherShortName * Subject
    | FormTeacher of SchoolClass * TeacherShortName
    | Custodian of TeacherShortName * Subject

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
        Decode.oneOf [
            Decode.field "normalTeacher" normalTeacherDecoder
            Decode.field "formTeacher" formTeacherDecoder
            Decode.field "custodian" custodianDecoder
        ]
