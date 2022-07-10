module ConsultationHours.HttpHandler

open ConsultationHours.DataTransferTypes
open Giraffe

let getConsultationHours (sokratesApi: Sokrates.SokratesApi) (untis: Untis.UntisExport) : HttpHandler =
    fun next ctx -> task {
        let untisTeachingData = untis.GetTeachingData()
        let! sokratesTeachers = sokratesApi.FetchTeachers

        let consultationHours =
            untisTeachingData
            |> List.choose (function
                | Untis.NormalTeacher (_, teacherShortName, _) -> Some teacherShortName
                | Untis.FormTeacher (_, teacherShortName) -> Some teacherShortName
                | Untis.Custodian _
                | Untis.Informant _ -> None
            )
            |> List.distinct
            |> List.map (fun teacherShortName ->
                let sokratesTeacher =
                    let (Untis.TeacherShortName shortName) = teacherShortName
                    sokratesTeachers
                    |> List.tryFind (fun (t: Sokrates.Teacher) -> CIString t.ShortName = CIString shortName)
                {
                    Teacher =
                        {
                            ShortName = (let (Untis.TeacherShortName t) = teacherShortName in t)
                            FirstName = sokratesTeacher |> Option.map (fun t -> t.FirstName) |> Option.defaultValue ""
                            LastName = sokratesTeacher |> Option.map (fun t -> t.LastName) |> Option.defaultValue ""
                        }
                    Subjects =
                        untisTeachingData
                        |> List.choose (function
                            | Untis.NormalTeacher (Untis.SchoolClass schoolClass, teacher, subject) when teacher = teacherShortName ->
                                Some {
                                    Class = schoolClass
                                    Subject = { ShortName = subject.ShortName; FullName = subject.FullName }
                                }
                            | Untis.NormalTeacher _
                            | Untis.FormTeacher _
                            | Untis.Custodian _
                            | Untis.Informant _ -> None
                        )
                        |> List.distinct
                    FormTeacherOfClasses =
                        untisTeachingData
                        |> List.choose (function
                            | Untis.FormTeacher (Untis.SchoolClass schoolClass, teacher) when teacher = teacherShortName -> Some schoolClass
                            | Untis.FormTeacher _
                            | Untis.NormalTeacher _
                            | Untis.Custodian _
                            | Untis.Informant _ -> None
                        )
                    Details =
                        untisTeachingData
                        |> List.tryPick (function
                            | Untis.Informant (teacher, room, workingDay, timeFrame) when teacher = teacherShortName ->
                                Some {
                                    DayOfWeek = Untis.WorkingDay.toGermanString workingDay
                                    BeginTime = timeFrame.BeginTime
                                    EndTime = timeFrame.EndTime
                                    Location = { ShortName = room.ShortName; FullName = room.FullName }
                                }
                            | Untis.Informant _
                            | Untis.FormTeacher _
                            | Untis.NormalTeacher _
                            | Untis.Custodian _ -> None
                        )
                }
            )
        return! Successful.OK consultationHours next ctx
    }