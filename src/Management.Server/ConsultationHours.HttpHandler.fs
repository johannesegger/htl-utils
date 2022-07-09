module ConsultationHours.HttpHandler

open ConsultationHours.DataTransferTypes
open Giraffe

let getConsultationHours (sokratesApi: Sokrates.SokratesApi) untisConfig : HttpHandler =
    fun next ctx -> task {
        let untisTeachingData = Untis.Core.getTeachingData |> Reader.run untisConfig
        let! sokratesTeachers = sokratesApi.FetchTeachers

        let consultationHours =
            untisTeachingData
            |> List.choose (function
                | Untis.Domain.NormalTeacher (_, teacherShortName, _) -> Some teacherShortName
                | Untis.Domain.FormTeacher (_, teacherShortName) -> Some teacherShortName
                | Untis.Domain.Custodian _
                | Untis.Domain.Informant _ -> None
            )
            |> List.distinct
            |> List.map (fun teacherShortName ->
                let sokratesTeacher =
                    let (Untis.Domain.TeacherShortName shortName) = teacherShortName
                    sokratesTeachers
                    |> List.tryFind (fun (t: Sokrates.Teacher) -> CIString t.ShortName = CIString shortName)
                {
                    Teacher =
                        {
                            ShortName = (let (Untis.Domain.TeacherShortName t) = teacherShortName in t)
                            FirstName = sokratesTeacher |> Option.map (fun t -> t.FirstName) |> Option.defaultValue ""
                            LastName = sokratesTeacher |> Option.map (fun t -> t.LastName) |> Option.defaultValue ""
                        }
                    Subjects =
                        untisTeachingData
                        |> List.choose (function
                            | Untis.Domain.NormalTeacher (Untis.Domain.SchoolClass schoolClass, teacher, subject) when teacher = teacherShortName ->
                                Some {
                                    Class = schoolClass
                                    Subject = { ShortName = subject.ShortName; FullName = subject.FullName }
                                }
                            | Untis.Domain.NormalTeacher _
                            | Untis.Domain.FormTeacher _
                            | Untis.Domain.Custodian _
                            | Untis.Domain.Informant _ -> None
                        )
                        |> List.distinct
                    FormTeacherOfClasses =
                        untisTeachingData
                        |> List.choose (function
                            | Untis.Domain.FormTeacher (Untis.Domain.SchoolClass schoolClass, teacher) when teacher = teacherShortName -> Some schoolClass
                            | Untis.Domain.FormTeacher _
                            | Untis.Domain.NormalTeacher _
                            | Untis.Domain.Custodian _
                            | Untis.Domain.Informant _ -> None
                        )
                    Details =
                        untisTeachingData
                        |> List.tryPick (function
                            | Untis.Domain.Informant (teacher, room, workingDay, timeFrame) when teacher = teacherShortName ->
                                Some {
                                    DayOfWeek = Untis.Domain.WorkingDay.toGermanString workingDay
                                    BeginTime = timeFrame.BeginTime
                                    EndTime = timeFrame.EndTime
                                    Location = { ShortName = room.ShortName; FullName = room.FullName }
                                }
                            | Untis.Domain.Informant _
                            | Untis.Domain.FormTeacher _
                            | Untis.Domain.NormalTeacher _
                            | Untis.Domain.Custodian _ -> None
                        )
                }
            )
        return! Successful.OK consultationHours next ctx
    }