open System.IO

let dryRun = false

let adApi = AD.ADApi.FromEnvironment()

type Teacher = {
    ShortName: string
    FirstName: string
    LastName: string
}

type Student = {
    SchoolClass: string
    Id: string
    FirstName: string
    LastName: string
}

let prepareTeacherPhotos baseDir teachers =
    let teacherMap = teachers |> List.map (fun (t: Teacher) -> sprintf "%s_%s" t.LastName t.FirstName |> CIString, t.ShortName) |> Map.ofList
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
        if not dryRun then
            File.Move(Path.Combine(baseDir, source), Path.Combine(baseDir, destination))
    )

let prepareStudentPhotos baseDir students =
    let studentMap =
        students
        |> List.map (fun s ->
            let key = (CIString s.SchoolClass, CIString (sprintf "%s_%s" s.LastName s.FirstName))
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
        |> Option.map (fun studentId ->
            Path.Combine(studentClass, fileName), sprintf "%s%s" studentId fileExtension
        )
    )
    |> Seq.iter (fun (source, destination) ->
        printfn "%s -> %s" source destination
        if not dryRun then
            File.Move(Path.Combine(baseDir, source), Path.Combine(baseDir, destination))
    )

let updatePhotos existingPhotosPath newPhotosPath names =
    Directory.GetFiles(newPhotosPath)
    |> Seq.iter (fun file ->
        printfn "Update photo of %s" (Path.GetFileNameWithoutExtension(file))
        if not dryRun then
            File.Move(file, Path.Combine(existingPhotosPath, Path.GetFileName(file)), overwrite=true)
    )

    Directory.GetFiles(existingPhotosPath)
    |> Seq.filter (fun file ->
        let fileName = Path.GetFileNameWithoutExtension(file)
        names
        |> List.exists (fun name -> CIString name = CIString fileName)
        |> not
    )
    |> Seq.iter (fun file ->
        printfn "Remove photo of %s" (Path.GetFileNameWithoutExtension(file))
        if not dryRun then
            File.Delete(file)
    )

[<EntryPoint>]
let main argv =
    let users = adApi.GetUsers ()

    let newTeacherPhotosPath = Environment.getEnvVarOrFail "NEW_TEACHER_PHOTOS_PATH"
    let existingTeacherPhotosPath = Environment.getEnvVarOrFail "EXISTING_TEACHER_PHOTOS_PATH"
    let teachers =
        users
        |> List.choose (fun user ->
            match user.Type with
            | AD.Teacher ->
                Some {
                    ShortName = (let (AD.UserName userName) = user.Name in userName)
                    FirstName = user.FirstName
                    LastName = user.LastName
                }
            | _ -> None
        )
    prepareTeacherPhotos newTeacherPhotosPath teachers
    updatePhotos existingTeacherPhotosPath newTeacherPhotosPath (teachers |> List.map (fun t -> t.ShortName))

    let newStudentPhotosPath = Environment.getEnvVarOrFail "NEW_STUDENT_PHOTOS_PATH"
    let existingStudentPhotosPath = Environment.getEnvVarOrFail "EXISTING_STUDENT_PHOTOS_PATH"
    let students =
        users
        |> List.choose (fun user ->
            match user.Type, user.SokratesId with
            | AD.Student (AD.GroupName schoolClass), Some (AD.SokratesId sokratesId) ->
                Some {
                    SchoolClass = schoolClass
                    Id = sokratesId
                    FirstName = user.FirstName
                    LastName = user.LastName
                }
            | _ -> None
        )
    prepareStudentPhotos existingStudentPhotosPath students
    updatePhotos existingStudentPhotosPath newStudentPhotosPath (students |> List.map (fun t -> t.Id))
    0
