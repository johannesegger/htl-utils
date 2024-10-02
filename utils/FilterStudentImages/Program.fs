open PhotoLibrary.Domain
open Sokrates
open System
open System.IO

let photoTargetDir = Directory.CreateDirectory(@".\schueler")

let sokratesApi = SokratesApi.FromEnvironment()
let photoLibraryConfig = PhotoLibrary.Configuration.Config.fromEnvironment ()
let sokratesStudents = sokratesApi.FetchStudents None None |> Async.RunSynchronously
sokratesStudents
|> List.filter (fun v -> v.Gender = Female)
|> List.sortBy(fun v -> v.SchoolClass, v.LastName, v.FirstName1)
|> List.map (fun v ->
    let (SokratesId sokratesId) = v.Id
    let photo =
        PhotoLibrary.Core.tryGetStudentPhoto sokratesId (None, None)
        |> Reader.run photoLibraryConfig
        |> Option.map (_.Data >> fun (Base64EncodedImage data) -> Convert.FromBase64String data)
    (v, photo)
)
|> List.iter (fun (student, photo) ->
    match photo with
    | Some photo ->
        let targetPath = Path.Combine(photoTargetDir.FullName, $"%s{student.SchoolClass} %s{student.LastName} %s{student.FirstName1}.jpg")
        File.WriteAllBytes(targetPath, photo)
    | None ->
        printfn $"%s{student.SchoolClass} %s{student.LastName} %s{student.FirstName1}"
)

