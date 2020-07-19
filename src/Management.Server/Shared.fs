namespace Shared

open System
#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

module AADGroupUpdates =
    type UserId = UserId of string

    module UserId =
        let encode (UserId userId) = Encode.string userId
        let decoder : Decoder<_> = Decode.string |> Decode.map UserId

    type GroupId = GroupId of string

    module GroupId =
        let encode (GroupId userId) = Encode.string userId
        let decoder : Decoder<_> = Decode.string |> Decode.map GroupId

    type User =
        {
            Id: UserId
            ShortName: string
            FirstName: string
            LastName: string
        }

    module User =
        let encode u =
            Encode.object [
                "id", UserId.encode u.Id
                "shortName", Encode.string u.ShortName
                "firstName", Encode.string u.FirstName
                "lastName", Encode.string u.LastName
            ]
        let decoder : Decoder<_> =
            Decode.object (fun get ->
                {
                    Id = get.Required.Field "id" UserId.decoder
                    ShortName = get.Required.Field "shortName" Decode.string
                    FirstName = get.Required.Field "firstName" Decode.string
                    LastName = get.Required.Field "lastName" Decode.string
                }
            )

    type MemberUpdates =
        {
            AddMembers: User list
            RemoveMembers: User list
        }

    module MemberUpdates =
        let encode memberUpdates =
            Encode.object [
                "addMembers", (List.map User.encode >> Encode.list) memberUpdates.AddMembers
                "removeMembers", (List.map User.encode >> Encode.list) memberUpdates.RemoveMembers
            ]
        let decoder : Decoder<_> =
            Decode.object (fun get -> {
                AddMembers = get.Required.Field "addMembers" (Decode.list User.decoder)
                RemoveMembers = get.Required.Field "removeMembers" (Decode.list User.decoder)
            })

    type Group = {
        Id: GroupId
        Name: string
    }

    module Group =
        let encode u =
            Encode.object [
                "id", GroupId.encode u.Id
                "name", Encode.string u.Name
            ]
        let decoder : Decoder<_> =
            Decode.object (fun get ->
                {
                    Id = get.Required.Field "id" GroupId.decoder
                    Name = get.Required.Field "name" Decode.string
                }
            )

    type GroupUpdate =
        | CreateGroup of string * User list
        | UpdateGroup of Group * MemberUpdates
        | DeleteGroup of Group

    module GroupUpdate =
        let encode = function
            | CreateGroup (name, members) -> Encode.object [ "createGroup", Encode.tuple2 Encode.string (List.map User.encode >> Encode.list) (name, members) ]
            | UpdateGroup (group, memberUpdates) -> Encode.object [ "updateGroup", Encode.tuple2 Group.encode MemberUpdates.encode (group, memberUpdates) ]
            | DeleteGroup group -> Encode.object [ "deleteGroup", Group.encode group ]
        let decoder : Decoder<_> =
            Decode.oneOf [
                Decode.field "createGroup" (Decode.tuple2 Decode.string (Decode.list User.decoder)) |> Decode.map CreateGroup
                Decode.field "updateGroup" (Decode.tuple2 Group.decoder MemberUpdates.decoder) |> Decode.map UpdateGroup
                Decode.field "deleteGroup" Group.decoder|> Decode.map DeleteGroup
            ]

module ConsultationHours =
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
