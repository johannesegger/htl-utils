module FileStorage.Mapping

open FileStorage.Domain

module Bytes =
    let toDto (Bytes v) = Shared.InspectDirectory.Bytes v

module FileInfo =
    let toDto path (fileInfo: FileInfo) =
        let path' = path @ [ fileInfo.Name ]
        {
            Shared.InspectDirectory.FileInfo.Path = String.concat "/" path'
            Shared.InspectDirectory.FileInfo.Size = Bytes.toDto fileInfo.Size
            Shared.InspectDirectory.FileInfo.CreationTime = fileInfo.CreationTime
            Shared.InspectDirectory.FileInfo.LastAccessTime = fileInfo.LastAccessTime
            Shared.InspectDirectory.FileInfo.LastWriteTime = fileInfo.LastWriteTime
        }

module DirectoryInfo =
    let toDto basePath v =
        let rec fn path directoryInfo =
            let path' = path @ [ directoryInfo.Name ]
            {
                Shared.InspectDirectory.DirectoryInfo.Path = String.concat "/" path'
                Shared.InspectDirectory.DirectoryInfo.Directories = directoryInfo.Directories |> List.map (fn path')
                Shared.InspectDirectory.DirectoryInfo.Files = directoryInfo.Files |> List.map (FileInfo.toDto path')
            }
        fn basePath v
