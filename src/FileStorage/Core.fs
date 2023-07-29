module FileStorage.Core

open FileStorage.Configuration
open FileStorage.Domain
open System
open System.IO

let private virtualPathToRealPath (v: string) = reader {
    let path = v.Split('\\', '/') |> List.ofArray
    match path with
    | [] | [""] -> return Error EmptyPath
    | baseDirectory :: pathTail ->
        let! config = Reader.environment
        match Map.tryFind baseDirectory config.BaseDirectories with
        | Some baseDirectory ->
            return Path.Combine([| baseDirectory; yield! pathTail |]) |> Ok // TODO verify that absolute pathTail works as expected
        | None -> return Error (InvalidBaseDirectory baseDirectory)
}

let private createStudentDirectories parentDirectory names =
    names
    |> List.map (fun name ->
        let path = Path.Combine(parentDirectory, name)
        try
            Directory.CreateDirectory path |> ignore
            Ok path
        with e ->
            Error { DirectoryName = name; ErrorMessage = e.Message }
    )
    |> Result.sequenceA
    |> Result.mapError CreatingSomeDirectoriesFailed

let private fileInfo path =
    let info = System.IO.FileInfo path
    {
        Name = info.Name
        Size = Bytes info.Length
        CreationTime = info.CreationTime
        LastAccessTime = info.LastAccessTime
        LastWriteTime = info.LastWriteTime
    }

let rec private directoryInfo path =
    let childDirectories =
        path
        |> Directory.GetDirectories
        |> Seq.map directoryInfo
        |> Seq.toList
    let childFiles =
        path
        |> Directory.GetFiles
        |> Seq.map fileInfo
        |> Seq.toList
    {
        Name = Path.GetFileName path
        Directories = childDirectories
        Files = childFiles
    }

let getChildDirectories path = reader {
    match! virtualPathToRealPath path with
    | Ok path ->
        try
            return
                path
                |> Directory.GetDirectories
                |> Seq.map Path.GetFileName
                |> Seq.toList
                |> Ok
        with
            | :? DirectoryNotFoundException -> return Ok []
            | e -> return Error (EnumeratingDirectoryFailed e.Message)
    | Error EmptyPath ->
        let! config = Reader.environment
        return
            config.BaseDirectories
            |> Map.toList
            |> List.map fst
            |> Ok
    | Error (InvalidBaseDirectory _ as e) -> return Error (GetChildDirectoriesError.PathMappingFailed e)
}

let createExerciseDirectories path names = reader {
    let! realPath = virtualPathToRealPath path
    return
        realPath
        |> Result.mapError PathMappingFailed
        |> Result.bind (fun path -> createStudentDirectories path names)
}

let getDirectoryInfo path = reader {
    let! realPath = virtualPathToRealPath path
    return
        realPath
        |> Result.mapError GetChildDirectoriesError.PathMappingFailed
        |> Result.bind (fun realPath ->
            try
                let result = directoryInfo realPath
                let dirName = Path.GetFileName path
                Ok { result with Name = if dirName = String.Empty then path else dirName }
            with e -> Error (EnumeratingDirectoryFailed e.Message)
        )
}
