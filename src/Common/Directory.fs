module Directory

open System.IO

let delete path =
    try
        // Remove read-only attributes of files and folders
        Directory.GetDirectories(path, "*", SearchOption.AllDirectories) |> Seq.iter (fun path -> File.SetAttributes(path, FileAttributes.Normal))
        Directory.GetFiles(path, "*", SearchOption.AllDirectories) |> Seq.iter (fun path -> File.SetAttributes(path, FileAttributes.Normal))

        Directory.Delete(path, true)
    with :? DirectoryNotFoundException -> ()
