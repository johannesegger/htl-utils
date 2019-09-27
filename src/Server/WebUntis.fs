module WebUntis

open System
open System.Collections.Generic
open System.Net.Http

let login (httpClient: HttpClient) = async {
    use request = new HttpRequestMessage(HttpMethod.Post, Uri(Environment.WebUntis.baseUrl, "j_spring_security_check"))
    request.Content <-
        let parameters =
            [
                "school", Environment.WebUntis.schoolName
                "j_username", Environment.WebUntis.username
                "j_password", Environment.WebUntis.password
                "token", ""
            ]
            |> List.map KeyValuePair.Create
        let content = new FormUrlEncodedContent(parameters)
        content
    let! response = httpClient.SendAsync request |> Async.AwaitTask
    response.EnsureSuccessStatusCode() |> ignore
    return ()
}

type Teacher = {
    Id: string
    ShortName: string
    FirstName: string
    LastName: string
}

type private WebUntisTeacher = FSharp.Data.JsonProvider<"data\\webuntis\\timetable-pageconfig-teachers.json">

let getTeachers (httpClient: HttpClient) (date: DateTime) = async {
    use request = new HttpRequestMessage(HttpMethod.Get, Uri(Environment.WebUntis.baseUrl, sprintf "api/public/timetable/weekly/pageconfig?type=2&date=%s" (date.ToString "yyyy-MM-dd")))
    let! response = httpClient.SendAsync request |> Async.AwaitTask
    response.EnsureSuccessStatusCode() |> ignore
    let! responseContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
    return
        WebUntisTeacher.Parse(responseContent).Data.Elements
        |> Seq.map (fun e -> { Id = string e.Id; ShortName = e.Name; FirstName = e.Forename; LastName = e.LongName })
        |> Seq.toList
}

type private WebUntisTeacherTimetable = FSharp.Data.JsonProvider<"data\\webuntis\\timetable-teacher.json">

type GetClassNamesFromTeacherTimetableError =
    | IrregularWeek of DateTime

let tryGetClassNamesFromTeacherTimetable (httpClient: HttpClient) (date: DateTime) teacherId = async {
    use request = new HttpRequestMessage(HttpMethod.Get, Uri(Environment.WebUntis.baseUrl, sprintf "api/public/timetable/weekly/data?elementType=2&elementId=%s&date=%s&formatId=8" teacherId (date.ToString "yyyy-MM-dd")))
    let! response = httpClient.SendAsync request |> Async.AwaitTask
    let! responseContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
    let timetable = WebUntisTeacherTimetable.Parse(responseContent).Data.Result.Data
    let periods =
        timetable.ElementPeriods.JsonValue
        |> fun periods -> FSharp.Data.JsonExtensions.GetProperty (periods, teacherId)
        |> FSharp.Data.JsonExtensions.AsArray
        |> Array.map WebUntisTeacherTimetable.``116``

    let isAllStandard =
        periods
        |> Seq.forall (fun p -> p.Is.Standard |> Option.defaultValue false)
    if isAllStandard then
        let elementTypeClass = 1
        let classIds =
            timetable.Elements
            |> Seq.filter (fun p -> p.Type = elementTypeClass)
            |> Seq.map (fun p -> p.Id)
            |> Seq.distinct
        return
            classIds
            |> Seq.choose (fun classId ->
                timetable.Elements
                |> Seq.find (fun p -> (p.Type, p.Id) = (elementTypeClass, classId))
                |> fun p -> Class.tryParse p.Name
            )
            |> Set.ofSeq
            |> Ok
    else
        return Error (IrregularWeek date)
}

let tryGetClassNamesFromTeacherTimetableInInterval httpClient dateFrom dateTo teacherId =
    let rec fn date errors = async {
        if date > dateTo then return Error (List.rev errors)
        else
            match! tryGetClassNamesFromTeacherTimetable httpClient date teacherId with
            | Ok classNames -> return Ok classNames
            | Error e -> return! fn (date.AddDays 7.) (e :: errors)
    }
    fn dateFrom []

let getClassesWithTeachers httpClient date = async {
    let! teachers = getTeachers httpClient date
    let! teachersWithClasses =
        teachers
        |> List.map (fun t -> async {
            match! tryGetClassNamesFromTeacherTimetableInInterval httpClient date (date.AddMonths 2) t.Id with
            | Ok classNames -> return Ok (t, classNames)
            | Error errors -> return Error (t, errors)
        })
        |> Async.Parallel
    match Result.sequence teachersWithClasses with
    | Ok teachersWithClasses ->
        let classes =
            teachersWithClasses
            |> List.map snd
            |> Set.unionMany
        return
            classes
            |> Seq.map (fun ``class`` ->
                let teachers =
                    teachersWithClasses
                    |> List.filter (snd >> Set.contains ``class``)
                    |> List.map fst
                (``class``, teachers)
            )
            |> Seq.toList
            |> Ok
    | Error es -> return Error es
}

type private WebUntisClass = FSharp.Data.JsonProvider<"data\\webuntis\\timetable-pageconfig-classes.json">

type Class = {
    Id: string
    Name: string
    ClassTeacherShortName: string option
}

let getClasses (httpClient: HttpClient) (date: DateTime) = async {
    use request = new HttpRequestMessage(HttpMethod.Get, Uri(Environment.WebUntis.baseUrl, sprintf "api/public/timetable/weekly/pageconfig?type=1&date=%s" (date.ToString "yyyy-MM-dd")))
    let! response = httpClient.SendAsync request |> Async.AwaitTask
    response.EnsureSuccessStatusCode() |> ignore
    let! responseContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
    return
        WebUntisClass.Parse(responseContent).Data.Elements
        |> Seq.map (fun e -> {
            Id = string e.Id
            Name = e.Name
            ClassTeacherShortName = e.Classteacher |> Option.map (fun t -> t.Name)
        })
        |> Seq.toList
}
