namespace KnowName.Server.Controllers

open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open System

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

[<ApiController>]
[<Route("/api/person")>]
[<Authorize("ReadPersonData")>]
type PersonController (sokratesApi: Sokrates.SokratesApi, photoLibraryConfig: PhotoLibrary.Configuration.Config, logger : ILogger<PersonController>) =
    inherit ControllerBase()

    [<HttpGet("groups")>]
    member this.GetGroups() = async {
        let! teachers = async {
            let! teachers = sokratesApi.FetchTeachers
            let teachersWithPhotos =
                PhotoLibrary.Core.getTeachersWithPhotos
                |> Reader.run photoLibraryConfig
                |> List.map CIString
                |> Set.ofList
            let persons =
                teachers
                |> List.sortBy (fun v -> v.LastName, v.FirstName)
                |> List.map (fun (teacher: Sokrates.Teacher) ->
                    let displayName = $"%s{teacher.ShortName} - %s{teacher.LastName.ToUpper()} %s{teacher.FirstName}"
                    let imageUrl =
                        if Set.contains (CIString teacher.ShortName) teachersWithPhotos
                        then Some (this.Url.Action(nameof this.GetTeacherPhoto, {| shortName = teacher.ShortName |}))
                        else None
                    DataTransfer.person displayName imageUrl
                )
            return DataTransfer.personGroup "Lehrer" persons
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
                        |> List.map (fun (PhotoLibrary.Domain.SokratesId studentId) -> Sokrates.SokratesId studentId)
                        |> Set.ofList
                    let persons =
                        students
                        |> List.sortBy (fun v -> v.LastName, v.FirstName1)
                        |> List.map (fun student ->
                            let displayName = $"%s{student.LastName.ToUpper()} %s{student.FirstName1}"
                            let imageUrl =
                                if Set.contains student.Id studentsWithPhotos
                                then
                                    let (Sokrates.SokratesId studentId) = student.Id
                                    Some (this.Url.Action(nameof this.GetStudentPhoto, {| studentId = studentId |}))
                                else None
                            DataTransfer.person displayName imageUrl
                        )
                    DataTransfer.personGroup className persons
                )
        }
        return [
            [ teachers ]
            yield! students |> List.groupBy (_.DisplayName >> Seq.tryHead) |> List.map snd
        ]
    }

    [<HttpGet("groups/teachers/{shortName}/photo")>]
    member this.GetTeacherPhoto(shortName: string, [<FromQuery>]width: Nullable<int>, [<FromQuery>]height: Nullable<int>) = async {
        match PhotoLibrary.Core.tryGetTeacherPhoto shortName (Option.ofNullable width, Option.ofNullable height) |> Reader.run photoLibraryConfig with
        | Some teacherPhoto ->
            let (PhotoLibrary.Domain.Base64EncodedImage data) = teacherPhoto.Data
            let bytes = Convert.FromBase64String data
            return this.File(bytes, "image/jpeg") :> IActionResult
        | None ->
            return this.NotFound()
    }

    [<HttpGet("groups/students/{studentId}/photo")>]
    member this.GetStudentPhoto(studentId: string, [<FromQuery>]width: Nullable<int>, [<FromQuery>]height: Nullable<int>) = async {
        match PhotoLibrary.Core.tryGetStudentPhoto studentId (Option.ofNullable width, Option.ofNullable height) |> Reader.run photoLibraryConfig with
        | Some studentPhoto ->
            let (PhotoLibrary.Domain.Base64EncodedImage data) = studentPhoto.Data
            let bytes = Convert.FromBase64String data
            return this.File(bytes, "image/jpeg") :> IActionResult
        | None ->
            return this.NotFound()
    }
