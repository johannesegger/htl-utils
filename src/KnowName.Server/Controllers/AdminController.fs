namespace KnowName.Server.Controllers

open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open System
open System.IO

module Admin =
    module Photos =
        let private getFileContent (file: IFormFile) = async {
            use stream = new MemoryStream()
            do! file.CopyToAsync(stream) |> Async.AwaitTask
            return stream.ToArray()
        }
        let getFromUploadedFile (file: IFormFile) = async {
            let! fileContent = getFileContent file
            match PhotoLibrary.Core.tryLoad fileContent with
            | Some image ->
                let name = Path.GetFileNameWithoutExtension(file.FileName)
                return Some (name, image)
            | None -> return None
        }

        let getFromUploadedFiles files = async {
            let! result =
                files
                |> Seq.map getFromUploadedFile
                |> Async.Sequential
            return result |> Array.choose id |> Array.toList
        }

    module DataTransfer =
        type Person = {
            DisplayName: string
            ImageUrl: string option
        }
        let person displayName imageUrl = { DisplayName = displayName; ImageUrl = imageUrl }

        type PersonGroup = {
            DisplayName: string
            Persons: Person list
        }
        let personGroup displayName persons = { DisplayName = displayName; Persons = persons }

        type Settings = {
            PersonGroups: PersonGroup list
        }
        let settings personGroups = { PersonGroups = personGroups }

        type UpdatePhotosResult = {
            UpdatedTeacherPhotos: string list
            UpdatedStudentPhotos: string list
        }
        let updatePhotoResult photoUpdates =
            {
                UpdatedTeacherPhotos =
                    photoUpdates
                    |> List.choose (function
                        | PhotoLibrary.Domain.AddPhoto (PhotoLibrary.Domain.TeacherPhoto name, _) -> Some name
                        | _ -> None
                    )
                UpdatedStudentPhotos =
                    photoUpdates
                    |> List.choose (function
                        | PhotoLibrary.Domain.AddPhoto (PhotoLibrary.Domain.StudentPhoto name, _) -> Some name
                        | _ -> None
                    )
            }

[<ApiController>]
[<Route("/api/admin")>]
[<Authorize("ManageSettings")>]
type AdminController (sokratesApi: Sokrates.SokratesApi, photoLibraryConfig: PhotoLibrary.Configuration.Config, logger : ILogger<AdminController>) =
    inherit ControllerBase()

    [<HttpGet>]
    member this.GetSettings() = async {
        let! teachers = async {
            let! teachers = sokratesApi.FetchTeachers
            let teachersWithPhotos =
                PhotoLibrary.Core.getTeachersWithPhotos
                |> Reader.run photoLibraryConfig
                |> List.map Sokrates.SokratesId
                |> Set.ofList
            let persons =
                teachers
                |> List.sortBy (fun v -> PersonName v.LastName, PersonName v.FirstName)
                |> List.map (fun (teacher: Sokrates.Teacher) ->
                    let displayName = $"%s{teacher.ShortName} - %s{teacher.LastName.ToUpper()} %s{teacher.FirstName}"
                    let imageUrl =
                        if Set.contains teacher.Id teachersWithPhotos
                        then Some (this.Url.Action(nameof this.GetTeacherPhoto, {| teacherId = teacher.Id.Value |}))
                        else None
                    Admin.DataTransfer.person displayName imageUrl
                )
            return Admin.DataTransfer.personGroup "Lehrer" persons
        }
        let! students = async {
            let! classNames = sokratesApi.FetchClasses None
            let! students = sokratesApi.FetchStudents None None
            return
                classNames
                |> List.sortBy id
                |> List.map (fun className ->
                    let students = students |> List.filter (fun v -> v.SchoolClass = className)
                    let studentsWithPhotos =
                        PhotoLibrary.Core.getStudentsWithPhotos
                        |> Reader.run photoLibraryConfig
                        |> List.map Sokrates.SokratesId
                        |> Set.ofList
                    let persons =
                        students
                        |> List.sortBy (fun v -> PersonName v.LastName, PersonName v.FirstName1)
                        |> List.map (fun student ->
                            let displayName = $"%s{student.LastName.ToUpper()} %s{student.FirstName1}"
                            let imageUrl =
                                if Set.contains student.Id studentsWithPhotos
                                then Some (this.Url.Action(nameof this.GetStudentPhoto, {| studentId = student.Id.Value |}))
                                else None
                            Admin.DataTransfer.person displayName imageUrl
                        )
                    Admin.DataTransfer.personGroup className persons
                )
        }
        return Admin.DataTransfer.settings
            [
                teachers
                yield! students
            ]
    }

    [<HttpGet("groups/teachers/{teacherId}/photo")>]
    member this.GetTeacherPhoto(teacherId: string, [<FromQuery>]width: Nullable<int>, [<FromQuery>]height: Nullable<int>) = async {
        match PhotoLibrary.Core.tryGetTeacherPhoto teacherId (Option.ofNullable width, Option.ofNullable height) |> Reader.run photoLibraryConfig with
        | Some teacherPhoto ->
            let (PhotoLibrary.Domain.Base64EncodedJpgImage data) = teacherPhoto.Data
            let bytes = Convert.FromBase64String data
            return this.File(bytes, "image/jpeg") :> IActionResult
        | None ->
            return this.NotFound()
    }

    [<HttpGet("groups/students/{studentId}/photo")>]
    member this.GetStudentPhoto(studentId: string, [<FromQuery>]width: Nullable<int>, [<FromQuery>]height: Nullable<int>) = async {
        match PhotoLibrary.Core.tryGetStudentPhoto studentId (Option.ofNullable width, Option.ofNullable height) |> Reader.run photoLibraryConfig with
        | Some studentPhoto ->
            let (PhotoLibrary.Domain.Base64EncodedJpgImage data) = studentPhoto.Data
            let bytes = Convert.FromBase64String data
            return this.File(bytes, "image/jpeg") :> IActionResult
        | None ->
            return this.NotFound()
    }

    [<HttpPost("photos")>]
    [<RequestSizeLimit(104857600L)>] // 100MB
    member _.AddPhotos(files: IFormFile[]) = async {
        let! teacherPhotoNameMap = async {
            let! teachers = sokratesApi.FetchTeachers
            return teachers |> List.map (fun v -> CIString v.ShortName, v.Id.Value) |> Map.ofList
        }
        let! validStudentPhotoNames = async {
            let! students = sokratesApi.FetchStudents None None
            return students |> List.map (_.Id.Value >> CIString) |> Set.ofList
        }
        let! newPhotos = async {
            let! photos = Admin.Photos.getFromUploadedFiles files
            return photos
                |> List.choose (fun (name, image) ->
                    teacherPhotoNameMap |> Map.tryFind (CIString name) |> Option.map (fun teacherId -> PhotoLibrary.Domain.TeacherPhoto teacherId, image)
                    |> Option.orElse (
                        if validStudentPhotoNames |> Set.contains (CIString name) then Some (PhotoLibrary.Domain.StudentPhoto name, image)
                        else None
                    )
                )
                |> List.map PhotoLibrary.Domain.AddPhoto
        }
        let photoUpdates = [
            yield! newPhotos
        ]
        PhotoLibrary.Core.updates photoUpdates |> Reader.run photoLibraryConfig

        return Admin.DataTransfer.updatePhotoResult photoUpdates
    }
