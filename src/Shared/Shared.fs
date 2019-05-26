namespace Shared

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

module CreateStudentDirectories =
    type Input =
        {
            ClassName: string
            Path: string * string list
        }

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

    type FileInfo =
        {
            Path: string list
            Size: Bytes
            CreationTime: System.DateTime
            LastAccessTime: System.DateTime
            LastWriteTime: System.DateTime
        }

    type DirectoryInfo =
        {
            Path: string list
            Directories: DirectoryInfo list
            Files: FileInfo list
        }
    module DirectoryInfo =
        let rec fold fn state directoryInfo =
            let state' = fn state directoryInfo
            List.fold (fold fn) state' directoryInfo.Directories

        let rec encode : Encoder<_> =
            let encodeBytes : Encoder<Bytes> =
                fun (Bytes o) -> Encode.int64 o
            let encodeFileInfo : Encoder<FileInfo> =
                fun o ->
                    Encode.object
                        [
                            "path", (List.map Encode.string >> Encode.list) o.Path
                            "size", encodeBytes o.Size
                            "creationTime", Encode.datetime o.CreationTime
                            "lastAccessTime", Encode.datetime o.LastAccessTime
                            "lastWriteTime", Encode.datetime o.LastWriteTime
                        ]
            fun o ->
                Encode.object
                    [
                        "path", (List.map Encode.string >> Encode.list) o.Path
                        "directories", (List.map encode >> Encode.list) o.Directories
                        "files", (List.map encodeFileInfo >> Encode.list) o.Files
                    ]

        let rec decode : Decoder<_> =
            let bytesDecoder =
                Decode.int64
                |> Decode.map Bytes

            let fileInfoDecoder =
                Decode.object (fun get ->
                    {
                        Path = get.Required.Field "path" (Decode.list Decode.string)
                        Size = get.Required.Field "size" bytesDecoder
                        CreationTime = get.Required.Field "creationTime" Decode.datetime
                        LastAccessTime = get.Required.Field "lastAccessTime" Decode.datetime
                        LastWriteTime = get.Required.Field "lastWriteTime" Decode.datetime
                    }
                )

            Decode.object (fun get ->
                {
                    Path = get.Required.Field "path" (Decode.list Decode.string)
                    Directories = get.Required.Field "directories" (Decode.list decode)
                    Files = get.Required.Field "files" (Decode.list fileInfoDecoder)
                }
            )
