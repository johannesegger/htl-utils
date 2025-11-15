namespace IndividualTests.Server.Controllers

open AAD
open IndividualTests.Server
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Microsoft.Graph.Beta
open Sokrates
open System
open System.Text.RegularExpressions

[<AutoOpen>]
module Sync =
    type StudentIdentifierDto = {
        FullName: string option
        LastName: string option
        FirstName: string option
        ClassName: string
    }

    [<AutoOpen>]
    module Domain =
        type StudentName = FullName of string | LastNameFirstName of string * string

        type StudentIdentifier = private {
            Class: string
            Name: StudentName
        }

        type TeacherName = private TeacherName of string
        module TeacherName =
            let create (v: string) = TeacherName (v.Trim().ToUpperInvariant())

        module StudentIdentifier =
            let tryFromDto v =
                match v.FullName, v.LastName, v.FirstName with
                | _, Some lastName, Some firstName ->
                    Some {
                        Class = v.ClassName.Trim()
                        Name = LastNameFirstName (lastName.Trim(), firstName.Trim())
                    }
                | Some fullName, _, _ ->
                    Some {
                        Class = v.ClassName.Trim()
                        Name = FullName (fullName.Trim())
                    }
                | _ -> None

            let matches (sokratesStudent: Sokrates.Student) (v: StudentIdentifier) =
                let nameMatches =
                    match v.Name with
                    | FullName fullName ->
                        CIString fullName = CIString $"%s{sokratesStudent.LastName} %s{sokratesStudent.FirstName1}"
                    | LastNameFirstName (lastName, firstName) ->
                        CIString lastName = CIString sokratesStudent.LastName &&
                        CIString firstName = CIString sokratesStudent.FirstName1
                CIString v.Class = CIString sokratesStudent.SchoolClass && nameMatches

    module Serialize =
        let gender (v: Sokrates.Gender) =
            match v with
            | Male -> "m"
            | Female -> "f"

[<ApiController>]
[<Route("api/sync")>]
[<Authorize>]
type SyncController (graphClient: GraphServiceClient, sokratesApi: SokratesApi, config: IConfiguration, logger : ILogger<SyncController>) =
    inherit ControllerBase()

    let tryFindStudent (sokratesStudents: Sokrates.Student list) (sokratesStudentsAddress: Map<string, Sokrates.Address>) studentsMailLookup (student: StudentIdentifier) =
        sokratesStudents
        |> List.tryFind (fun v -> student |> StudentIdentifier.matches v)
        |> Option.map (fun sokratesStudent ->
            let (SokratesId sokratesId) = sokratesStudent.Id
            let mailAddress : string option = studentsMailLookup |> Map.tryFind sokratesId
            let address : Sokrates.Address option = sokratesStudentsAddress |> Map.tryFind sokratesId
            {|
                SokratesId = sokratesId
                LastName = sokratesStudent.LastName
                FirstName = sokratesStudent.FirstName1
                ClassName = sokratesStudent.SchoolClass
                MailAddress = mailAddress
                Gender = Serialize.gender sokratesStudent.Gender
                Address = address |> Option.map (fun v ->
                    {|
                        Country = v.Country
                        Zip = v.Zip
                        City = v.City
                        Street = v.Street
                    |}
                )
            |}
        )

    let tryFindTeacher (sokratesTeacherLookup: Map<TeacherName, Sokrates.Teacher>) (entraTeacherLookup: Map<TeacherName, Models.User>) (teacherName: TeacherName) =
        Map.tryFind teacherName entraTeacherLookup
        |> Option.map (fun teacher ->
            let gender = Map.tryFind teacherName sokratesTeacherLookup |> Option.bind (fun v -> v.Gender)
            {|
                ShortName = Regex.Replace(teacher.UserPrincipalName, "@.*$", "")
                LastName = teacher.Surname
                FirstName = teacher.GivenName
                MailAddress = teacher.Mail
                Gender = gender |> Option.map Serialize.gender 
            |}
        )

    [<HttpQuery>]
    [<Route("students")>]
    member _.SyncStudentData ([<FromBody>]students: StudentIdentifierDto list) = async {
        let! sokratesStudents =
            [ for offset in 0..-1..-6 do DateTime.Today.AddMonths(offset) ]
            |> List.map (fun date -> sokratesApi.FetchStudents None (Some date))
            |> Async.Parallel
            |> Async.map (Seq.collect id >> Seq.distinctBy _.Id >> Seq.toList)
        let! sokratesStudentAddresses =
            [ for offset in 0..-1..-6 do DateTime.Today.AddMonths(offset) ]
            |> List.map (fun date -> sokratesApi.FetchStudentAddresses (Some date))
            |> Async.Parallel
            |> Async.map (
                Seq.collect id
                >> Seq.distinctBy _.StudentId
                >> Seq.choose (fun (v: StudentAddress) ->
                    let (SokratesId sokratesId) = v.StudentId
                    v.Address |> Option.map (fun address -> (sokratesId, address))
                )
                >> Map.ofSeq
            )
        let studentsGroupId = config.["StudentsGroupId"]
        let sokratesIdAttributeName = config.["SokratesIdAttributeName"]
        let! studentsMailLookup = async {
            let! users =
                graphClient.Groups.[studentsGroupId].Members.GraphUser.GetAsync(fun o ->
                    o.QueryParameters.Select <- [| sokratesIdAttributeName; "mail" |]
                )
                |> graphClient.ReadAll<_, Models.User>
            return users
                |> Seq.choose (fun v ->
                    v.AdditionalData.TryGetValue(sokratesIdAttributeName)
                    |> Option.fromTryPattern
                    |> Option.bind ((fun v -> v :?> string) >> Option.ofObj)
                    |> Option.map (fun sokratesId -> sokratesId, v.Mail)
                )
                |> Map.ofSeq
        }
        return students
            |> List.map (fun student ->
                match StudentIdentifier.tryFromDto student |> Option.bind (tryFindStudent sokratesStudents sokratesStudentAddresses studentsMailLookup) with
                | Some data ->
                    {|
                        Type = "exact-match"
                        Name = student
                        Data = Some data
                    |}
                | None ->
                    {|
                        Type = "no-match"
                        Name = student
                        Data = None
                    |}
            )
    }

    [<HttpQuery>]
    [<Route("teachers")>]
    member _.SyncTeacherData ([<FromBody>]teacherShortNames: string list) = async {
        let! sokratesTeacherLookup = async {
            let! teachers = sokratesApi.FetchTeachers
            return teachers |> List.map (fun v -> (TeacherName.create v.ShortName, v)) |> Map.ofList
        }
        let teachersGroupId = config.["TeachersGroupId"]
        let! entraTeacherLookup = async {
            let! users =
                graphClient.Groups.[teachersGroupId].Members.GraphUser.GetAsync(fun o ->
                    o.QueryParameters.Select <- [| "userPrincipalName"; "surname"; "givenName"; "mail" |]
                )
                |> graphClient.ReadAll<_, Models.User>
            return users
                |> Seq.map (fun v ->
                    let teacherShortName = Regex.Replace(v.UserPrincipalName, "@.*$", "")
                    (TeacherName.create teacherShortName, v)
                )
                |> Map.ofSeq
        }
        return teacherShortNames
            |> List.map (fun shortName ->
                match tryFindTeacher sokratesTeacherLookup entraTeacherLookup (TeacherName.create shortName) with
                | Some data ->
                    {|
                        Type = "exact-match"
                        Name = shortName
                        Data = Some data
                    |}
                | None ->
                    {|
                        Type = "no-match"
                        Name = shortName
                        Data = None
                    |}
            )
    }
