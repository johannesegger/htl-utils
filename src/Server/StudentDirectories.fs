module StudentDirectories

open System.IO

type CreateDirectoryErrorInfo =
    { DirectoryName: string
      ErrorMessage: string }

type CreateDirectoriesErrorInfo =
    { BaseDirectory: string
      CreatedDirectories: string list
      NotCreatedDirectories: CreateDirectoryErrorInfo list}

type CreateStudentDirectoriesError =
    | InvalidBaseDirectory of string
    | GetStudentsError of string * string
    | CreatingSomeDirectoriesFailed of CreateDirectoriesErrorInfo

let createStudentDirectories getStudents baseDirectory className = async {
    printfn "Creating student directories for %s in %s" className baseDirectory
    let! students = async {
        try
            let! students = getStudents className
            return Ok students
        with e -> return GetStudentsError (className, e.ToString()) |> Error
    }
    
    return
        students
        |> Result.bind (
            List.map (fun (lastName, firstName) ->
                let name = sprintf "%s.%s" lastName firstName
                let path = Path.Combine(baseDirectory, name)
                try
                    Directory.CreateDirectory path |> ignore
                    Ok name
                with e ->
                    Error { DirectoryName = name; ErrorMessage = e.Message }
            )
            >> Result.partition
            >> function
            | x, [] -> Ok x
            | x, y ->
                let info =
                    { BaseDirectory = baseDirectory
                      CreatedDirectories = x
                      NotCreatedDirectories = y }
                Error (CreatingSomeDirectoriesFailed info)
        )
}