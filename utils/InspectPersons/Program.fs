open System

let adApi = AD.ADApi.FromEnvironment()
let untisApi = Untis.UntisExport.FromEnvironment()
let sokratesApi = Sokrates.SokratesApi.FromEnvironment()

let teachers = adApi.GetUsers(AD.Teacher)
let teachingData = untisApi.GetTeachingData()
let sokratesTeachers = sokratesApi.FetchTeachers |> Async.RunSynchronously

teachers
|> List.filter (fun adTeacher ->
    let (AD.UserName teacherShortName) = adTeacher.Name
    sokratesTeachers
    |> List.exists (fun sokratesTeacher -> CIString sokratesTeacher.ShortName = CIString teacherShortName)
)
|> List.choose (fun adTeacher ->
    let (AD.UserName teacherShortName) = adTeacher.Name
    let birthday = sokratesTeachers |> List.find (fun v -> CIString v.ShortName = CIString teacherShortName) |> fun v -> v.DateOfBirth
    let isFormTeacher =
        teachingData
        |> List.exists (fun teacherTask ->
            match teacherTask with
            | Untis.FormTeacher (schoolClass, Untis.TeacherShortName shortName)
                when CIString shortName = CIString teacherShortName -> true
            | _ -> false
        )
    let lessonsInWIIClasses = 
        teachingData
        |> List.choose (fun teacherTask ->
            match teacherTask with
            | Untis.NormalTeacher (Untis.SchoolClass schoolClass, Untis.TeacherShortName shortName, subject)
                when CIString shortName = CIString teacherShortName
                    && schoolClass.EndsWith("HWII", StringComparison.InvariantCultureIgnoreCase)
                    && not <| List.contains subject.ShortName ["ETAUTWP_4"; "FET1WP_3"; "FET1WP4"; "WLA"; "WPT_3"; "WPT_4"] -> Some (schoolClass, subject.FullName)
            | _ -> None
        )
        |> List.distinct
        |> List.sort
    if isFormTeacher then
        Some ((4, teacherShortName), $"{teacherShortName} ist KV.")
    elif birthday < DateTime(DateTime.Today.Year - 60, DateTime.Today.Month, DateTime.Today.Day) then
        Some ((2, teacherShortName), $"{teacherShortName} ist kein KV, aber schon über 60 Jahre alt.")
    elif not lessonsInWIIClasses.IsEmpty then
        let lessonText = lessonsInWIIClasses |> List.map (fun (schoolClass, subject) -> $"  * %s{schoolClass} - %s{subject}") |> String.concat Environment.NewLine
        Some ((1, teacherShortName), $"{teacherShortName} ist kein KV und unterrichtet in der WII:%s{Environment.NewLine}%s{lessonText}")
    else
        Some ((3, teacherShortName), $"{teacherShortName} ist zwar kein KV, unterrichtet aber dzt. nicht bzw. kein Theoriefach in der WII.")
)
|> List.sortBy fst
|> List.map snd
|> List.iter (printfn "%s")
