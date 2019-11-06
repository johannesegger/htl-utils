module App

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System
open Thoth.Json.Giraffe
open Thoth.Json.Net

// ---------------------------------
// Web app
// ---------------------------------

let handleGetAutoGroups : HttpHandler =
    fun next ctx -> Successful.OK () next ctx

let getAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let! untisTeachingData = Http.get ctx "http://untis/api/teaching-data" (Decode.list Untis.TeacherInClass.decoder) |> Async.StartChild
        let! sokratesTeachers = Http.get ctx "http://sokrates/api/teachers" (Decode.list Sokrates.Teacher.decoder) |> Async.StartChild
        let! finalThesesMentors = Http.get ctx "http://final-theses/api/mentors" (Decode.list FinalTheses.Mentor.decoder) |> Async.StartChild
        let! aadAutoGroups = Http.get ctx "http://aad/api/auto-groups" (Decode.list AAD.Group.decoder) |> Async.StartChild
        let! aadUsers = Http.get ctx "http://aad/api/users" (Decode.list AAD.User.decoder)

        let! untisTeachingData = untisTeachingData |> Async.map (Result.map (List.choose id))
        let! sokratesTeachers = sokratesTeachers
        let! finalThesesMentors = finalThesesMentors
        let! aadAutoGroups = aadAutoGroups

        let getUpdates aadUsers aadAutoGroups sokratesTeachers teachingData finalThesesMentors =
            let aadUserLookupByUserName =
                aadUsers
                |> List.map (fun (user: AAD.User) -> user.UserName, user.Id)
                |> Map.ofList

            let teacherIds =
                sokratesTeachers
                |> List.choose (fun (t: Sokrates.Teacher) -> Map.tryFind t.ShortName aadUserLookupByUserName)

            let classGroupsWithTeacherIds =
                teachingData
                |> List.map (function
                    | Untis.NormalTeacher (schoolClass, teacherShortName, _)
                    | Untis.FormTeacher (schoolClass, teacherShortName) -> (schoolClass, teacherShortName)
                )
                |> List.groupBy fst
                |> List.map (fun (Untis.SchoolClass schoolClass, teachers) ->
                    let teacherIds =
                        teachers
                        |> List.choose (snd >> fun (Untis.TeacherShortName v) -> Map.tryFind v aadUserLookupByUserName)
                        |> List.distinct
                    (sprintf "GrpLehrer%s" schoolClass, teacherIds)
                )

            let formTeacherIds =
                teachingData
                |> List.choose (function
                    | Untis.NormalTeacher _ -> None
                    | Untis.FormTeacher (schoolClass, Untis.TeacherShortName teacherShortName) -> Some teacherShortName
                )
                |> List.choose (flip Map.tryFind aadUserLookupByUserName)
                |> List.distinct

            let aadUserLookupByMailAddress =
                aadUsers
                |> List.collect (fun (user: AAD.User) ->
                    user.MailAddresses
                    |> List.map (fun mailAddress -> CIString mailAddress, user.Id)
                )
                |> Map.ofList

            let finalThesesMentorIds =
                finalThesesMentors
                |> List.choose (fun (m: FinalTheses.Mentor) -> Map.tryFind (CIString m.MailAddress) aadUserLookupByMailAddress)

            let professionalGroupsWithTeacherIds =
                [
                    "GrpD", [ "D" ]
                    "GrpE", [ "E1" ]
                    "GrpAM", [ "AM" ]
                    "GrpCAD", [ "KOBE"; "KOP1"; "MT"; "PLP" ]
                    "GrpWE", [ "ETAUTWP_4"; "FET1WP_3"; "FET1WP_4"; "WLA"; "WPT_3"; "WPT_4" ]
                ]
                |> List.map (Tuple.mapSnd (List.map CIString) >> fun (groupName, subjects) ->
                    let teacherIds =
                        teachingData
                        |> List.choose (function
                            | Untis.NormalTeacher (_, Untis.TeacherShortName teacherShortName, Untis.Subject subject) ->
                                Some (teacherShortName, subject)
                            | Untis.FormTeacher _ -> None
                        )
                        |> List.filter (snd >> fun subject ->
                            subjects |> List.contains (CIString subject)
                        )
                        |> List.choose (fst >> flip Map.tryFind aadUserLookupByUserName)
                        |> List.distinct
                    (groupName, teacherIds)
                )

            let desiredGroups = [
                ("GrpLehrer", teacherIds)
                ("GrpKV", formTeacherIds)
                ("GrpDA-Betreuer", finalThesesMentorIds)
                yield! classGroupsWithTeacherIds
                yield! professionalGroupsWithTeacherIds
            ]

            let aadUserLookupById =
                aadUsers
                |> List.map (fun (user: AAD.User) -> user.Id, AAD.User.toDto user)
                |> Map.ofList

            let aadAutoGroupsLookupById =
                aadAutoGroups
                |> List.map (fun (group: AAD.Group) -> group.Id, AAD.Group.toDto group)
                |> Map.ofList

            AADGroupUpdates.calculateAll aadAutoGroups desiredGroups
            |> List.map (AAD.GroupModification.toDto aadUserLookupById aadAutoGroupsLookupById)

        return!
            Ok getUpdates
            |> Result.apply (Result.mapError List.singleton aadUsers)
            |> Result.apply (Result.mapError List.singleton aadAutoGroups)
            |> Result.apply (Result.mapError List.singleton sokratesTeachers)
            |> Result.apply (Result.mapError List.singleton untisTeachingData)
            |> Result.apply (Result.mapError List.singleton finalThesesMentors)
            |> function
            | Ok v -> Successful.OK v next ctx
            | Error e -> ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let applyAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let! input = ctx.BindJsonAsync<Shared.AADGroupUpdates.GroupUpdate list>()
        let body =
            input
            |> List.map (AAD.GroupModification.fromDto >> AAD.GroupModification.encode)
            |> Encode.list
        let! result = Http.post ctx "http://aad/api/auto-groups/modify" body (Decode.nil ())
        match result with
        | Ok v -> return! Successful.OK () next ctx
        | Error e -> return! ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/aad/group-updates" >=> Auth.requiresAdmin >=> getAADGroupUpdates
                ]
                POST >=> choose [
                    route "/aad/group-updates/apply" >=> Auth.requiresAdmin >=> applyAADGroupUpdates
                ]
            ])
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    match env.IsDevelopment() with
    | true -> app.UseDeveloperExceptionPage() |> ignore
    | false -> app.UseGiraffeErrorHandler errorHandler |> ignore
    app.UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddHttpClient() |> ignore
    services.AddGiraffe() |> ignore
    let coders =
        Extra.empty
        |> Extra.withCustom Shared.AADGroupUpdates.GroupUpdate.encode Shared.AADGroupUpdates.GroupUpdate.decoder
    services.AddSingleton<IJsonSerializer>(ThothSerializer(isCamelCase = true, extra = coders)) |> ignore

let configureLogging (ctx: WebHostBuilderContext) (builder : ILoggingBuilder) =
    builder
        .AddFilter(fun l -> ctx.HostingEnvironment.IsDevelopment() || l.Equals LogLevel.Error)
        .AddConsole()
        .AddDebug()
    |> ignore

[<EntryPoint>]
let main _ =
    WebHostBuilder()
        .UseKestrel()
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0