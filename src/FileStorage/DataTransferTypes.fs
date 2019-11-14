module FileStorage.DataTransferTypes

open System
open Thoth.Json.Net

type CreateDirectoryErrorInfo =
    { DirectoryName: string
      ErrorMessage: string }
module CreateDirectoryErrorInfo =
    let encode v =
        Encode.object [
            "directoryName", Encode.string v.DirectoryName
            "errorMessage", Encode.string v.ErrorMessage
        ]

type PathMappingError =
    | EmptyPath
    | InvalidBaseDirectory of string
module PathMappingError =
    let encode  = function
        | EmptyPath -> Encode.object [ "emptyPath", Encode.nil ]
        | InvalidBaseDirectory name -> Encode.object [ "invalidBaseDirectory", Encode.string name ]

type GetChildDirectoriesError =
    | PathMappingFailed of PathMappingError
    | EnumeratingDirectoryFailed of message: string
module GetChildDirectoriesError =
    let encode = function
        | PathMappingFailed v -> PathMappingError.encode v
        | EnumeratingDirectoryFailed message -> Encode.object [ "emptyPath", Encode.string message ]

type CreateStudentDirectoriesError =
    | PathMappingFailed of PathMappingError
    | CreatingSomeDirectoriesFailed of CreateDirectoryErrorInfo list
module CreateStudentDirectoriesError =
    let encode = function
        | PathMappingFailed v -> PathMappingError.encode v
        | CreatingSomeDirectoriesFailed errorInfos -> Encode.object [ "creatingSomeDirectoriesFailed", (List.map CreateDirectoryErrorInfo.encode >> Encode.list) errorInfos ]

type CreateDirectoriesData =
    {
        Path: string
        Names: string list
    }
module CreateDirectoriesData =
    let encode v =
        Encode.object [
            "path", Encode.string v.Path
            "names", (List.map Encode.string >> Encode.list) v.Names
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get -> {
            Path = get.Required.Field "path" Decode.string
            Names = get.Required.Field "names" (Decode.list Decode.string)
        })

type Bytes = Bytes of int64
module Bytes =
    let encode (Bytes v) = Encode.int64 v
    let decoder : Decoder<_> = Decode.int64 |> Decode.map Bytes

type FileInfo = {
    Name: string
    Size: Bytes
    CreationTime: DateTime
    LastAccessTime: DateTime
    LastWriteTime: DateTime
}
module FileInfo =
    let encode v =
        Encode.object [
            "name", Encode.string v.Name
            "size", Bytes.encode v.Size
            "creationTime", Encode.datetime v.CreationTime
            "lastAccessTime", Encode.datetime v.LastAccessTime
            "lastWriteTime", Encode.datetime v.LastWriteTime
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get -> {
            Name = get.Required.Field "name" Decode.string
            Size = get.Required.Field "size" Bytes.decoder
            CreationTime = get.Required.Field "creationTime" Decode.datetime
            LastAccessTime = get.Required.Field "lastAccessTime" Decode.datetime
            LastWriteTime = get.Required.Field "lastWriteTime" Decode.datetime
        })

type DirectoryInfo = {
    Name: string
    Directories: DirectoryInfo list
    Files: FileInfo list
}
module DirectoryInfo =
    let rec encode v =
        Encode.object [
            "name", Encode.string v.Name
            "directories", (List.map encode >> Encode.list) v.Directories
            "files", (List.map FileInfo.encode >> Encode.list) v.Files
        ]
    let rec decoder : Decoder<_> =
        Decode.object (fun get -> {
            Name = get.Required.Field "name" Decode.string
            Directories = get.Required.Field "directories" (Decode.list decoder)
            Files = get.Required.Field "files" (Decode.list FileInfo.decoder)
        })
