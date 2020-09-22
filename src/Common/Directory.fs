module Directory

open System.IO

// see https://stackoverflow.com/a/16321356/1293659
let delete path =
    Directory.GetFiles(path, "*", SearchOption.AllDirectories) |> Seq.iter File.Delete
    Directory.Delete(path, true)
