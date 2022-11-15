open System.IO
open System.Text.RegularExpressions

let generateNamePlates templateDir dataRows replaceTemplateParams =
    let documentTemplate = File.ReadAllText(Path.Combine(templateDir, "document.html"))
    let itemTemplate = File.ReadAllText(Path.Combine(templateDir, "item.html"))
    dataRows
    |> Seq.map (replaceTemplateParams itemTemplate)
    |> String.concat ""
    |> fun items -> documentTemplate.Replace("%Zeilen%", items)
    |> fun content -> File.WriteAllText(Path.Combine(templateDir, "result.html"), content)

let generateTeacherNamePlates templateDir = async {
    let sokratesApi = Sokrates.SokratesApi.FromEnvironment()
    let untisExport = Untis.UntisExport.FromEnvironment()
    let activeTeachers =
        untisExport.GetTeachingData()
        |> List.map (function
            | Untis.NormalTeacher (_, Untis.TeacherShortName teacherShortName, _)
            | Untis.FormTeacher (_, Untis.TeacherShortName teacherShortName)
            | Untis.Custodian (Untis.TeacherShortName teacherShortName, _)
            | Untis.Informant (Untis.TeacherShortName teacherShortName, _, _, _) -> teacherShortName
        )
        |> List.distinct
        |> List.sort
    let! sokratesTeachers = sokratesApi.FetchTeachers
    let sokratesTeachersByShortName =
        sokratesTeachers
        |> List.map (fun v -> CIString v.ShortName, v)
        |> Map.ofList
    return
        activeTeachers
        |> List.choose (fun teacherName ->
            match sokratesTeachersByShortName |> Map.tryFind (CIString teacherName) with
            | Some v -> Some v
            | None ->
                printfn $"WARNING: Can't find %s{teacherName} in Sokrates"
                None
        )
        |> fun v ->
            generateNamePlates templateDir v (fun template (teacher: Sokrates.Teacher) ->
                let degreeFront =
                    let title = teacher.Title |> Option.defaultValue ""
                    let degreeFront = teacher.DegreeFront |> Option.defaultValue ""
                    $"%s{title} %s{degreeFront}".Trim()
                template
                    .Replace("%DegreeFront%", degreeFront)
                    .Replace("%DegreeBack%", teacher.DegreeBack |> Option.defaultValue "")
                    .Replace("%LastName%", teacher.LastName)
                    .Replace("%FirstName%", teacher.FirstName)
            )
}

let getYear (className: string) =
    className.Substring(0, 1)
    |> int

let getDepartment (className: string) =
    match className.IndexOf '_' with
    | -1 -> className.Substring(2)
    | idx -> className.Substring(2, idx - 2)

let getShortDepartment className =
    match getDepartment className with
    | "FMBM" -> "FS"
    | "HGTI" -> "GT"
    | "HMBT" -> "MB"
    | "HME" -> "ME"
    | "HWII" -> "WI"
    | "HWIM"
    | "HWIE" -> "WM"
    | "BMIS" -> "AS"
    | department -> failwithf "Unknown department \"%s\"" department

let getLongDepartment className =
    match getDepartment className with
    | "FMBM" -> "Fachschule"
    | "HGTI" -> "Gebäudetechnik"
    | "HMBT" -> "Maschinenbau"
    | "HME" -> "Mechatronik"
    | "HWII" -> "Betriebsinformatik"
    | "HWIM"
    | "HWIE" -> "Wirtschaftsingenieure"
    | department -> failwithf "Unknown department \"%s\"" department

let generateStudentNamePlates templateDir = async {
    let sokratesApi = Sokrates.SokratesApi.FromEnvironment()
    let! students = sokratesApi.FetchStudents None None
    return
        students
        |> List.filter (fun v -> not <| Regex.IsMatch(v.SchoolClass, "BMB$|VMB$"))
        |> List.sortBy(fun v -> v.SchoolClass, v.LastName, v.FirstName1)
        |> fun v ->
            generateNamePlates templateDir v (fun template student ->
                template
                    .Replace("%ClassName%", student.SchoolClass)
                    .Replace("%ClassGrade%", getYear student.SchoolClass |> string)
                    .Replace("%DepartmentId%", getDepartment student.SchoolClass)
                    .Replace("%DepartmentShort%", getShortDepartment student.SchoolClass)
                    .Replace("%DepartmentLong%", getLongDepartment student.SchoolClass)
                    .Replace("%LastName%", student.LastName)
                    .Replace("%FirstName%", student.FirstName1)
            )
}

[<EntryPoint>]
let main argv =
    match argv with
    | [| templateBaseDir |] ->
        Directory.GetDirectories(templateBaseDir)
        |> Seq.iter (fun templateDir ->
            let templateDirName = Path.GetFileName(templateDir)
            if templateDirName.Equals("teachers") then generateTeacherNamePlates templateDir |> Async.RunSynchronously
            elif templateDirName.Equals("students") then generateStudentNamePlates templateDir |> Async.RunSynchronously
            else printfn $"WARNING: Ignoring %s{templateDir}"
        )
        printfn "Done."
        printfn "Print result.html to PDF using Chromium based browser (no margin)"
        0
    | _ ->
        eprintfn "Usage: dotnet run -- path/to/data/dir"
        -1
