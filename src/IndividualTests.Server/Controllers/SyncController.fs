namespace IndividualTests.Server.Controllers

open IndividualTests.Server
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Microsoft.Graph

type StudentNameDto = {
    FullName: string option
    LastName: string option
    FirstName: string option
    ClassName: string
}

[<ApiController>]
[<Route("api/sync")>]
[<Authorize>]
type DataController (graphClient: GraphServiceClient, logger : ILogger<DataController>) =
    inherit ControllerBase()

    [<HttpQuery>]
    [<Route("students")>]
    member _.SyncStudentData ([<FromBody>]students: StudentNameDto list) = async {
        return students
            |> List.map (fun student -> 
                if student.FullName = Some "AKALOVIC Tobias" then
                    {|
                        Type = "exact-match"
                        Name = student
                        Data = Some {|
                            SokratesId = "1234"
                            LastName = "Akalovic"
                            FirstName = "Tobias"
                            ClassName = "4BHME"
                            MailAddress = "Tobias.Akalovic@htlvb.at"
                            Address = {|
                                Country = "Österreich"
                                Zip = "4810"
                                City = "Gmunden"
                                Street = "Linzerstraße 127"
                            |}
                        |}
                    |}
                elif student.FullName = Some "BAßANI SAM" then
                    {|
                        Type = "exact-match"
                        Name = student
                        Data = Some {|
                            SokratesId = "2345"
                            LastName = "Baßani"
                            FirstName = "Sam"
                            ClassName = "4AHME"
                            MailAddress = "Sam.Bassani@htlvb.at"
                            Address = {|
                                Country = "Österreich"
                                Zip = "4810"
                                City = "Gmunden"
                                Street = "Linzerstraße 135"
                            |}
                        |}
                    |}
                else
                    {|
                        Type = "no-match"
                        Name = student
                        Data = None
                    |}
            )
    }

    [<HttpQuery>]
    [<Route("teachers")>]
    member _.SyncStudentData ([<FromBody>]teacherShortNames: string list) = async {
        return teacherShortNames
            |> List.map (fun shortName ->
                if shortName = "EGGJ" then
                    {|
                        Type = "exact-match"
                        ShortName = shortName
                        Data = Some {|
                            LastName = "Egger"
                            FirstName = "Johannes"
                            MailAddress = "eggj@htlvb.at"
                            Gender = "m"
                        |}
                    |}
                else
                    {|
                        Type = "no-match"
                        ShortName = shortName
                        Data = None
                    |}
            )
    }
