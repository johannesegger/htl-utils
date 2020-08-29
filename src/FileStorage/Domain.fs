module FileStorage.Domain

open System

type CreateDirectoryErrorInfo = {
    DirectoryName: string
    ErrorMessage: string
}

type PathMappingError =
    | EmptyPath
    | InvalidBaseDirectory of string

type GetChildDirectoriesError =
    | PathMappingFailed of PathMappingError
    | EnumeratingDirectoryFailed of message: string

type CreateStudentDirectoriesError =
    | PathMappingFailed of PathMappingError
    | CreatingSomeDirectoriesFailed of CreateDirectoryErrorInfo list

type Bytes = Bytes of int64

type FileInfo = {
    Name: string
    Size: Bytes
    CreationTime: DateTime
    LastAccessTime: DateTime
    LastWriteTime: DateTime
}

type DirectoryInfo = {
    Name: string
    Directories: DirectoryInfo list
    Files: FileInfo list
}
