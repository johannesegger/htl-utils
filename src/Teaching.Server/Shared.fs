namespace Shared

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

module CreateStudentDirectories =
    type CreateDirectoriesData = {
        ClassName: string
        Path: string
    }
    module CreateDirectoriesData =
        let encode v =
            Encode.object [
                "className", Encode.string v.ClassName
                "path", Encode.string v.Path
            ]
        let decoder : Decoder<_> =
            Decode.object (fun get -> {
                ClassName = get.Required.Field "className" Decode.string
                Path = get.Required.Field "path" Decode.string
            })

module InspectDirectory =
#if FABLE_COMPILER
    [<Fable.Core.Erase>] // enables equality
#endif
    type Bytes = Bytes of int64
    module Bytes =
        let toHumanReadable (Bytes value) =
            let units = [ "B"; "KB"; "MB"; "GB" ]
            let rec fn units value =
                match units with
                | [] -> failwith "No units left to try."
                | [ name ] -> (value, name)
                | name :: xs ->
                    let value' = value / 1024.
                    if value' < 0.95 then (value, name)
                    else fn xs value'
            let (value, name) = fn units (float value)
            sprintf "%.2f %s" value name
        let encode : Encoder<_> =
            fun (Bytes v) -> Encode.int64 v
        let decoder : Decoder<_> =
            Decode.int64
            |> Decode.map Bytes

    type FileInfo =
        {
            Path: string
            Size: Bytes
            CreationTime: System.DateTime
            LastAccessTime: System.DateTime
            LastWriteTime: System.DateTime
        }
    module FileInfo =
        let encode : Encoder<_> =
            fun v ->
                Encode.object
                    [
                        "path", Encode.string v.Path
                        "size", Bytes.encode v.Size
                        "creationTime", Encode.datetime v.CreationTime
                        "lastAccessTime", Encode.datetime v.LastAccessTime
                        "lastWriteTime", Encode.datetime v.LastWriteTime
                    ]
        let decoder : Decoder<_> =
            Decode.object (fun get ->
                {
                    Path = get.Required.Field "path" Decode.string
                    Size = get.Required.Field "size" Bytes.decoder
                    CreationTime = get.Required.Field "creationTime" Decode.datetime
                    LastAccessTime = get.Required.Field "lastAccessTime" Decode.datetime
                    LastWriteTime = get.Required.Field "lastWriteTime" Decode.datetime
                }
            )

    type DirectoryInfo =
        {
            Path: string
            Directories: DirectoryInfo list
            Files: FileInfo list
        }
    module DirectoryInfo =
        let rec encode : Encoder<_> =
            fun o ->
                Encode.object
                    [
                        "path", Encode.string o.Path
                        "directories", (List.map encode >> Encode.list) o.Directories
                        "files", (List.map FileInfo.encode >> Encode.list) o.Files
                    ]

        let rec decoder : Decoder<_> =
            Decode.object (fun get ->
                {
                    Path = get.Required.Field "path" Decode.string
                    Directories = get.Required.Field "directories" (Decode.list decoder)
                    Files = get.Required.Field "files" (Decode.list FileInfo.decoder)
                }
            )
