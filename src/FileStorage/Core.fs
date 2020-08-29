module FileStorage.Core

open FileStorage.Domain
open System
open System.IO

let private baseDirectories =
    Environment.getEnvVarOrFail "FILE_STORAGE_BASE_DIRECTORIES"
    |> String.split ";"
    |> Seq.chunkBySize 2
    |> Seq.map (fun s -> s.[0], s.[1])
    |> Map.ofSeq

let private virtualPathToRealPath (v: string) =
    let path = v.Split('\\', '/') |> List.ofArray
    match path with
    | [] | [""] -> Error EmptyPath
    | baseDirectory :: pathTail ->
        match baseDirectories |> Map.tryFind baseDirectory with
        | Some baseDirectory ->
            Path.Combine([| baseDirectory; yield! pathTail |]) // TODO verify that absolute pathTail works as expected
            |> Ok
        | None -> Error (InvalidBaseDirectory baseDirectory)

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
    |> Result.sequence
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

let getChildDirectories path =
    match virtualPathToRealPath path with
    | Ok path ->
        try
            path
            |> Directory.GetDirectories
            |> Seq.map Path.GetFileName
            |> Seq.toList
            |> Ok
        with
            | :? DirectoryNotFoundException -> Ok []
            | e -> Error (EnumeratingDirectoryFailed e.Message)
    | Error EmptyPath -> baseDirectories |> Map.toList |> List.map fst |> Ok
    | Error (InvalidBaseDirectory _ as e) -> Error (GetChildDirectoriesError.PathMappingFailed e)

let createExerciseDirectories path names =
    virtualPathToRealPath path
    |> Result.mapError PathMappingFailed
    |> Result.bind (fun path -> createStudentDirectories path names)

let getDirectoryInfo path =
    virtualPathToRealPath path
    |> Result.mapError GetChildDirectoriesError.PathMappingFailed
    |> Result.bind (fun realPath ->
        try
            let result = directoryInfo realPath
            let dirName = Path.GetFileName path
            Ok { result with Name = if dirName = String.Empty then path else dirName }
        with e -> Error (EnumeratingDirectoryFailed e.Message)
    )
