module FileStorage

open System
open Thoth.Json.Net

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

type Bytes = Bytes of int64
module Bytes =
    let decoder : Decoder<_> = Decode.int64 |> Decode.map Bytes
    let toDto (Bytes v) = Shared.InspectDirectory.Bytes v

type FileInfo = {
    Name: string
    Size: Bytes
    CreationTime: DateTime
    LastAccessTime: DateTime
    LastWriteTime: DateTime
}
module FileInfo =
    let decoder : Decoder<_> =
        Decode.object (fun get -> {
            Name = get.Required.Field "name" Decode.string
            Size = get.Required.Field "size" Bytes.decoder
            CreationTime = get.Required.Field "creationTime" Decode.datetime
            LastAccessTime = get.Required.Field "lastAccessTime" Decode.datetime
            LastWriteTime = get.Required.Field "lastWriteTime" Decode.datetime
        })
    let toDto path fileInfo =
        let path' = path @ [ fileInfo.Name ]
        {
            Shared.InspectDirectory.FileInfo.Path = String.concat "/" path'
            Shared.InspectDirectory.FileInfo.Size = Bytes.toDto fileInfo.Size
            Shared.InspectDirectory.FileInfo.CreationTime = fileInfo.CreationTime
            Shared.InspectDirectory.FileInfo.LastAccessTime = fileInfo.LastAccessTime
            Shared.InspectDirectory.FileInfo.LastWriteTime = fileInfo.LastWriteTime
        }

type DirectoryInfo = {
    Name: string
    Directories: DirectoryInfo list
    Files: FileInfo list
}
module DirectoryInfo =
    let rec decoder : Decoder<_> =
        Decode.object (fun get -> {
            Name = get.Required.Field "name" Decode.string
            Directories = get.Required.Field "directories" (Decode.list decoder)
            Files = get.Required.Field "files" (Decode.list FileInfo.decoder)
        })
    let toDto basePath v =
        let rec fn path directoryInfo =
            let path' = path @ [ directoryInfo.Name ]
            {
                Shared.InspectDirectory.DirectoryInfo.Path = String.concat "/" path'
                Shared.InspectDirectory.DirectoryInfo.Directories = directoryInfo.Directories |> List.map (fn path')
                Shared.InspectDirectory.DirectoryInfo.Files = directoryInfo.Files |> List.map (FileInfo.toDto path')
            }
        fn basePath v
