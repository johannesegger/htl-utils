open System.IO
open System.Net.Http
open System.Net.Http.Json
open System.Text.RegularExpressions

let generateNamePlates templateDir dataRows replaceTemplateParams =
    let documentTemplate = File.ReadAllText(Path.Combine(templateDir, "document.html"))
    let itemTemplate = File.ReadAllText(Path.Combine(templateDir, "item.html"))
    dataRows
    |> Seq.map (replaceTemplateParams itemTemplate)
    |> String.concat ""
    |> fun items -> documentTemplate.Replace("%Zeilen%", items)
    |> fun content -> File.WriteAllText(Path.Combine(templateDir, "result.html"), content)

type GenderizeResult = {
    Count: int
    Gender: string
    Name: string
    Probability: float
}

type Gender = Male | Female
module Gender =
    let tryParse v =
        if CIString v = CIString "male" then Some Male
        elif CIString v = CIString "female" then Some Female
        else None

let getMainFirstName (name: string) =
    match name.IndexOf ' ' with
    | -1 -> name
    | v -> name.Substring(0, v)

let fetchGenders (teachers: Sokrates.Teacher list) =
    teachers
    |> List.map (fun v -> getMainFirstName v.FirstName)
    |> List.distinct
    |> List.chunkBySize 10
    |> List.map (fun names -> async {
        let namesParams =
            names
            |> List.map (fun v -> $"name[]=%s{v}")
            |> String.concat "&"
        use httpClient = new HttpClient()
        let url = $"https://api.genderize.io/?%s{namesParams}&country_id=AT"
        let! genders = httpClient.GetFromJsonAsync<GenderizeResult list>(url) |> Async.AwaitTask
        return
            genders
            |> List.map (fun v ->
                let gender =
                    match Gender.tryParse v.Gender with
                    | Some v -> v
                    | None when v.Name = "Ehrenfried" -> Male
                    | None -> failwith $"Invalid gender for \"%s{v.Name}\": \"%s{v.Gender}\""
                v.Name, gender
            )
    })
    |> Async.Parallel
    |> Async.map (Seq.collect id >> Map.ofSeq)

let genderTitle gender (v: string) =
    match gender with
    | Male -> v
    | Female ->
        v.Split(' ')
        |> Array.map (fun v ->
            if CIString v = CIString "Dr." then "Dr.ⁱⁿ"
            elif CIString v = CIString "DI" then "DIⁱⁿ"
            elif CIString v = CIString "DI(FH)" then "DIⁱⁿ(FH)"
            elif CIString v = CIString "(FH)" then "(FH)" // e.g. "DI (FH)"
            elif CIString v = CIString "DDI" then "DDIⁱⁿ"
            elif CIString v = CIString "Mag." then "Mag.ᵃ"
            elif CIString v = CIString "Mag.(FH)" then "Mag.ᵃ(FH)"
            elif CIString v = CIString "MMag." then "MMag.ᵃ"
            elif CIString v = CIString "Ing." then "Ing.ⁱⁿ"
            elif CIString v = CIString "Priv.-Doz." then "Priv.-Doz.ⁱⁿ"
            elif CIString v = CIString "Prof." then "Prof.ⁱⁿ"
            elif CIString v = CIString "Professor" then "Professorin"
            elif CIString v = CIString "OStR" then "OStRⁱⁿ"
            elif CIString v = CIString "OStR." then "OStR.ⁱⁿ"
            elif CIString v = CIString "AV" then "AVⁱⁿ"
            elif CIString v = CIString "Direktor" then "Direktorin"
            elif CIString v = CIString "VL" then "VLⁱⁿ"
            elif CIString v = CIString "FOL" then "FOLⁱⁿ"
            elif CIString v = CIString "Dipl.-Päd." then "Dipl.-Päd.ⁱⁿ"
            elif CIString v = CIString "BSc" then "BSc"
            elif CIString v = CIString "BSc." then "BSc."
            elif CIString v = CIString "MSc" then "MSc"
            elif CIString v = CIString "MSc." then "MSc."
            elif CIString v = CIString "BEd" then "BEd"
            elif CIString v = CIString "BEd." then "BEd."
            elif CIString v = CIString "MEd" then "MEd"
            elif CIString v = CIString "MEd." then "MEd."
            elif CIString v = CIString "BA" then "BA"
            elif CIString v = CIString "MA" then "MA"
            elif CIString v = CIString "MLBT" then "MLBT"
            else failwith $"Unknown title \"%s{v}\""
        )
        |> String.concat " "

let generateTeacherNamePlates templateDir = async {
    let sokratesApi = Sokrates.SokratesApi.FromEnvironment()
    let! teachers = async {
        let! list = sokratesApi.FetchTeachers
        return list |> List.sortBy (fun v -> v.LastName, v.FirstName)
    }
    let! nameToGender = fetchGenders teachers
    generateNamePlates templateDir teachers (fun template (teacher: Sokrates.Teacher) ->
        let gender = nameToGender |> Map.tryFind (getMainFirstName teacher.FirstName) |> Option.defaultWith (fun () -> failwith $"Can't find gender of \"%A{teacher}\"")
        let degreeFront =
            let title = teacher.Title |> Option.map (genderTitle gender) |> Option.defaultValue ""
            let degreeFront = teacher.DegreeFront |> Option.map (genderTitle gender) |> Option.defaultValue ""
            $"%s{title} %s{degreeFront}".Trim()
        let degreeBack = teacher.DegreeBack |> Option.map (genderTitle gender) |> Option.defaultValue ""
        template
            .Replace("%DegreeFront%", degreeFront)
            .Replace("%DegreeBack%", degreeBack)
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
    let! sokratesStudents = sokratesApi.FetchStudents None None
    let students =
        sokratesStudents
        |> List.filter (fun v -> not <| Regex.IsMatch(v.SchoolClass, "BMB$|VMB$"))
        // |> List.filter (fun v -> getYear v.SchoolClass > 2)
        |> List.sortBy(fun v -> v.SchoolClass, v.LastName, v.FirstName1)
    generateNamePlates templateDir students (fun template student ->
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
