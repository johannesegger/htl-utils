namespace KnowName.Server.Controllers

open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open System

module Game =
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
                    Game.DataTransfer.person displayName imageUrl
                )
            return Game.DataTransfer.personGroup "Lehrer" persons
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
                            Game.DataTransfer.person displayName imageUrl
                        )
                    Game.DataTransfer.personGroup className persons
                )
        }
        return [
            [ teachers ]
            yield! students |> List.groupBy (_.DisplayName >> Seq.tryHead) |> List.map snd
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
