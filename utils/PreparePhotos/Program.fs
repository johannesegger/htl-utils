open System
open System.IO
open System.Net
open System.Net.Http
open Thoth.Json.Net

module Http =
    type FetchError =
        | HttpError of url: string * HttpStatusCode * content: string
        | DecodeError of url: string * message: string

    let get (url: string) decoder = async {
        use httpClient = new HttpClient()
        use requestMessage = new HttpRequestMessage(HttpMethod.Get, url)

        use! response = httpClient.SendAsync(requestMessage) |> Async.AwaitTask
        let! responseContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        if not response.IsSuccessStatusCode then
            return Error (HttpError (url, response.StatusCode, responseContent))
        else
            return
                Decode.fromString decoder responseContent
                |> Result.mapError (fun message -> DecodeError(url, message))
    }

let prepareTeacherPhotos baseDir = async {
    let! sokratesTeacherResponse = Http.get "http://localhost:3001/api/teachers" (Decode.list Sokrates.DataTransferTypes.Teacher.decoder)
    match sokratesTeacherResponse with
    | Ok teachers ->
        let teacherMap = teachers |> List.map (fun t -> sprintf "%s_%s" t.LastName t.FirstName |> CIString, t.ShortName) |> Map.ofList
        Directory.GetFiles baseDir
        |> Seq.choose (fun file ->
            let fileName = Path.GetFileName file
            let teacherName = Path.GetFileNameWithoutExtension file
            let fileExtension = Path.GetExtension(file).ToLowerInvariant()
            Map.tryFind (CIString teacherName) teacherMap
            |> Option.map (fun teacherShortName -> fileName, sprintf "%s%s" teacherShortName fileExtension)
        )
        |> Seq.iter (fun (source, destination) ->
            printfn "%s -> %s" source destination
            File.Move(Path.Combine(baseDir, source), Path.Combine(baseDir, destination))
        )
    | Error (Http.HttpError (url, statusCode, content)) -> eprintfn "Can't load teachers from %s (%O)" url statusCode
    | Error (Http.DecodeError (url, message)) -> eprintfn "Can't decode teachers: %s" message
}

let prepareStudentPhotos baseDir = async {
    let! sokratesStudentResponse = Http.get "http://localhost:3001/api/students" (Decode.list Sokrates.DataTransferTypes.Student.decoder)
    match sokratesStudentResponse with
    | Ok students ->
        let studentMap =
            students
            |> List.map (fun s ->
                let key = (CIString s.SchoolClass, CIString (sprintf "%s_%s" s.LastName s.FirstName1))
                (key, s.Id)
            )
            |> Map.ofList
        Directory.GetFiles(baseDir, "*", SearchOption.AllDirectories)
        |> Seq.choose (fun file ->
            let studentClass = Path.GetDirectoryName file |> Path.GetFileName
            let fileName = Path.GetFileName file
            let studentName = Path.GetFileNameWithoutExtension file
            let fileExtension = Path.GetExtension(file)
            Map.tryFind (CIString studentClass, CIString studentName) studentMap
            |> Option.map (fun (Sokrates.DataTransferTypes.SokratesId studentId) ->
                Path.Combine(studentClass, fileName), sprintf "%s%s" studentId fileExtension
            )
        )
        |> Seq.iter (fun (source, destination) ->
            printfn "%s -> %s" source destination
            File.Move(Path.Combine(baseDir, source), Path.Combine(baseDir, destination))
        )
    | Error (Http.HttpError (url, statusCode, content)) -> eprintfn "Can't load teachers from %s (%O)" url statusCode
    | Error (Http.DecodeError (url, message)) -> eprintfn "Can't decode teachers: %s" message
}

[<EntryPoint>]
let main argv =
    async {
        do! prepareTeacherPhotos @"..\data\photo-library\teacher-photos"
        do! prepareStudentPhotos @"..\data\photo-library\student-photos"
        return 0
    }
    |> Async.RunSynchronously
