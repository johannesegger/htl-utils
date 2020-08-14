module ConsultationHours.HttpHandler

open ConsultationHours.DataTransferTypes
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Thoth.Json.Net

let getConsultationHours : HttpHandler =
    fun next ctx -> task {
        let! untisTeachingData = Http.get ctx (ServiceUrl.untis "teaching-data") (Decode.list Untis.DataTransferTypes.TeacherTask.decoder) |> Async.StartChild
        let! sokratesTeachers = Http.get ctx (ServiceUrl.sokrates "teachers") (Decode.list Sokrates.DataTransferTypes.Teacher.decoder) |> Async.StartChild

        let! untisTeachingData = untisTeachingData
        let! sokratesTeachers = sokratesTeachers

        let fn untisTeachingData sokratesTeachers =
            untisTeachingData
            |> List.choose (function
                | Untis.DataTransferTypes.NormalTeacher (_, teacherShortName, _) -> Some teacherShortName
                | Untis.DataTransferTypes.FormTeacher (_, teacherShortName) -> Some teacherShortName
                | Untis.DataTransferTypes.Custodian _
                | Untis.DataTransferTypes.Informant _ -> None
            )
            |> List.distinct
            |> List.map (fun teacherShortName ->
                let sokratesTeacher =
                    let (Untis.DataTransferTypes.TeacherShortName shortName) = teacherShortName
                    sokratesTeachers
                    |> List.tryFind (fun (t: Sokrates.DataTransferTypes.Teacher) -> CIString t.ShortName = CIString shortName)
                {
                    Teacher =
                        {
                            ShortName = (let (Untis.DataTransferTypes.TeacherShortName t) = teacherShortName in t)
                            FirstName = sokratesTeacher |> Option.map (fun t -> t.FirstName) |> Option.defaultValue ""
                            LastName = sokratesTeacher |> Option.map (fun t -> t.LastName) |> Option.defaultValue ""
                        }
                    Subjects =
                        untisTeachingData
                        |> List.choose (function
                            | Untis.DataTransferTypes.NormalTeacher (Untis.DataTransferTypes.SchoolClass schoolClass, teacher, subject) when teacher = teacherShortName ->
                                Some {
                                    Class = schoolClass
                                    Subject = { ShortName = subject.ShortName; FullName = subject.FullName }
                                }
                            | Untis.DataTransferTypes.NormalTeacher _
                            | Untis.DataTransferTypes.FormTeacher _
                            | Untis.DataTransferTypes.Custodian _
                            | Untis.DataTransferTypes.Informant _ -> None
                        )
                        |> List.distinct
                    FormTeacherOfClasses =
                        untisTeachingData
                        |> List.choose (function
                            | Untis.DataTransferTypes.FormTeacher (Untis.DataTransferTypes.SchoolClass schoolClass, teacher) when teacher = teacherShortName -> Some schoolClass
                            | Untis.DataTransferTypes.FormTeacher _
                            | Untis.DataTransferTypes.NormalTeacher _
                            | Untis.DataTransferTypes.Custodian _
                            | Untis.DataTransferTypes.Informant _ -> None
                        )
                    Details =
                        untisTeachingData
                        |> List.tryPick (function
                            | Untis.DataTransferTypes.Informant (teacher, room, workingDay, timeFrame) when teacher = teacherShortName ->
                                Some {
                                    DayOfWeek = Untis.DataTransferTypes.WorkingDay.toGermanString workingDay
                                    BeginTime = timeFrame.BeginTime
                                    EndTime = timeFrame.EndTime
                                    Location = { ShortName = room.ShortName; FullName = room.FullName }
                                }
                            | Untis.DataTransferTypes.Informant _
                            | Untis.DataTransferTypes.FormTeacher _
                            | Untis.DataTransferTypes.NormalTeacher _
                            | Untis.DataTransferTypes.Custodian _ -> None
                        )
                }
            )
        return!
            Ok fn
            |> Result.apply (Result.mapError List.singleton untisTeachingData)
            |> Result.apply (Result.mapError List.singleton sokratesTeachers)
            |> function
            | Ok v -> Successful.OK v next ctx
            | Error e -> ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }