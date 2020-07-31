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

let getAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let! untisTeachingData = Http.get ctx (ServiceUrl.untis "teaching-data") (Decode.list Untis.DataTransferTypes.TeacherTask.decoder) |> Async.StartChild
        let! sokratesTeachers = Http.get ctx (ServiceUrl.sokrates "teachers") (Decode.list Sokrates.DataTransferTypes.Teacher.decoder) |> Async.StartChild
        let! finalThesesMentors = Http.get ctx (ServiceUrl.finalTheses "mentors") (Decode.list FinalTheses.DataTransferTypes.Mentor.decoder) |> Async.StartChild
        let! aadAutoGroups = Http.get ctx (ServiceUrl.aad "auto-groups") (Decode.list AAD.DataTransferTypes.Group.decoder) |> Async.StartChild
        let! aadUsers = Http.get ctx (ServiceUrl.aad "users") (Decode.list AAD.DataTransferTypes.User.decoder)

        let! untisTeachingData = untisTeachingData
        let! sokratesTeachers = sokratesTeachers
        let! finalThesesMentors = finalThesesMentors
        let! aadAutoGroups = aadAutoGroups

        let getUpdates aadUsers aadAutoGroups sokratesTeachers teachingData finalThesesMentors =
            let aadUserLookupByUserName =
                aadUsers
                |> List.map (fun (user: AAD.DataTransferTypes.User) -> user.UserName, user.Id)
                |> Map.ofList

            let teacherIds =
                sokratesTeachers
                |> List.choose (fun (t: Sokrates.DataTransferTypes.Teacher) -> Map.tryFind t.ShortName aadUserLookupByUserName)

            let classGroupsWithTeacherIds =
                teachingData
                |> List.choose (function
                    | Untis.DataTransferTypes.NormalTeacher (schoolClass, teacherShortName, _)
                    | Untis.DataTransferTypes.FormTeacher (schoolClass, teacherShortName) -> Some (schoolClass, teacherShortName)
                    | Untis.DataTransferTypes.Custodian _
                    | Untis.DataTransferTypes.Informant _ -> None
                )
                |> List.groupBy fst
                |> List.map (fun (Untis.DataTransferTypes.SchoolClass schoolClass, teachers) ->
                    let teacherIds =
                        teachers
                        |> List.choose (snd >> fun (Untis.DataTransferTypes.TeacherShortName v) -> Map.tryFind v aadUserLookupByUserName)
                        |> List.distinct
                    (sprintf "GrpLehrer%s" schoolClass, teacherIds)
                )

            let formTeacherIds =
                teachingData
                |> List.choose (function
                    | Untis.DataTransferTypes.FormTeacher (_, Untis.DataTransferTypes.TeacherShortName teacherShortName) -> Some teacherShortName
                    | Untis.DataTransferTypes.NormalTeacher _
                    | Untis.DataTransferTypes.Custodian _
                    | Untis.DataTransferTypes.Informant _ -> None
                )
                |> List.choose (flip Map.tryFind aadUserLookupByUserName)
                |> List.distinct

            let aadUserLookupByMailAddress =
                aadUsers
                |> List.collect (fun (user: AAD.DataTransferTypes.User) ->
                    user.MailAddresses
                    |> List.map (fun mailAddress -> CIString mailAddress, user.Id)
                )
                |> Map.ofList

            let finalThesesMentorIds =
                finalThesesMentors
                |> List.choose (fun (m: FinalTheses.DataTransferTypes.Mentor) -> Map.tryFind (CIString m.MailAddress) aadUserLookupByMailAddress)

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
                            | Untis.DataTransferTypes.NormalTeacher (_, Untis.DataTransferTypes.TeacherShortName teacherShortName, subject) ->
                                Some (teacherShortName, subject)
                            | Untis.DataTransferTypes.FormTeacher _
                            | Untis.DataTransferTypes.Custodian _
                            | Untis.DataTransferTypes.Informant _ -> None
                        )
                        |> List.filter (snd >> fun subject ->
                            subjects |> List.contains (CIString subject.ShortName)
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
                |> List.map (fun (user: AAD.DataTransferTypes.User) -> user.Id, AADTypeMapping.User.toDto user)
                |> Map.ofList

            let aadAutoGroupsLookupById =
                aadAutoGroups
                |> List.map (fun (group: AAD.DataTransferTypes.Group) -> group.Id, AADTypeMapping.Group.toDto group)
                |> Map.ofList

            AADGroupUpdates.calculateAll aadAutoGroups desiredGroups
            |> List.map (AADTypeMapping.GroupModification.toDto aadUserLookupById aadAutoGroupsLookupById)

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
            |> List.map (AADTypeMapping.GroupModification.fromDto >> AAD.DataTransferTypes.GroupModification.encode)
            |> Encode.list
        let! result = Http.post ctx (ServiceUrl.aad "auto-groups/modify") body (Decode.nil ())
        match result with
        | Ok () -> return! Successful.OK () next ctx
        | Error e -> return! ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let getConsultationHours : HttpHandler =
    fun next ctx -> task {
        let! untisTeachingData = Http.get ctx (ServiceUrl.untis "teaching-data") (Decode.list Untis.DataTransferTypes.TeacherTask.decoder) |> Async.StartChild
        let! sokratesTeachers = Http.get ctx (ServiceUrl.sokrates "teachers") (Decode.list Sokrates.DataTransferTypes.Teacher.decoder) |> Async.StartChild

        let! untisTeachingData = untisTeachingData
        let! sokratesTeachers = sokratesTeachers

        let fn untisTeachingData sokratesTeachers =
            untisTeachingData
            |> List.choose (function
                | Untis.DataTransferTypes.NormalTeacher (_, teacherShortName, _) -> Some teacherShortName
                | Untis.DataTransferTypes.FormTeacher (_, teacherShortName) -> Some teacherShortName
                | Untis.DataTransferTypes.Custodian _
                | Untis.DataTransferTypes.Informant _ -> None
            )
            |> List.distinct
            |> List.map (fun teacherShortName ->
                let sokratesTeacher =
                    let (Untis.DataTransferTypes.TeacherShortName shortName) = teacherShortName
                    sokratesTeachers
                    |> List.tryFind (fun (t: Sokrates.DataTransferTypes.Teacher) -> CIString t.ShortName = CIString shortName)
                {
                    Shared.ConsultationHours.Teacher =
                        {
                            ShortName = (let (Untis.DataTransferTypes.TeacherShortName t) = teacherShortName in t)
                            FirstName = sokratesTeacher |> Option.map (fun t -> t.FirstName) |> Option.defaultValue ""
                            LastName = sokratesTeacher |> Option.map (fun t -> t.LastName) |> Option.defaultValue ""
                        }
                    Shared.ConsultationHours.Subjects =
                        untisTeachingData
                        |> List.choose (function
                            | Untis.DataTransferTypes.NormalTeacher (Untis.DataTransferTypes.SchoolClass schoolClass, teacher, subject) when teacher = teacherShortName ->
                                Some {
                                    Shared.ConsultationHours.TeacherSubject.Class = schoolClass
                                    Shared.ConsultationHours.TeacherSubject.Subject = { ShortName = subject.ShortName; FullName = subject.FullName }
                                }
                            | Untis.DataTransferTypes.NormalTeacher _
                            | Untis.DataTransferTypes.FormTeacher _
                            | Untis.DataTransferTypes.Custodian _
                            | Untis.DataTransferTypes.Informant _ -> None
                        )
                        |> List.distinct
                    Shared.ConsultationHours.FormTeacherOfClasses =
                        untisTeachingData
                        |> List.choose (function
                            | Untis.DataTransferTypes.FormTeacher (Untis.DataTransferTypes.SchoolClass schoolClass, teacher) when teacher = teacherShortName -> Some schoolClass
                            | Untis.DataTransferTypes.FormTeacher _
                            | Untis.DataTransferTypes.NormalTeacher _
                            | Untis.DataTransferTypes.Custodian _
                            | Untis.DataTransferTypes.Informant _ -> None
                        )
                    Shared.ConsultationHours.Details =
                        untisTeachingData
                        |> List.tryPick (function
                            | Untis.DataTransferTypes.Informant (teacher, room, workingDay, timeFrame) when teacher = teacherShortName ->
                                Some {
                                    Shared.ConsultationHours.ConsultationHourDetails.DayOfWeek = Untis.DataTransferTypes.WorkingDay.toGermanString workingDay
                                    Shared.ConsultationHours.ConsultationHourDetails.BeginTime = timeFrame.BeginTime
                                    Shared.ConsultationHours.ConsultationHourDetails.EndTime = timeFrame.EndTime
                                    Shared.ConsultationHours.ConsultationHourDetails.Location = { ShortName = room.ShortName; FullName = room.FullName }
                                }
                            | Untis.DataTransferTypes.Informant _
                            | Untis.DataTransferTypes.FormTeacher _
                            | Untis.DataTransferTypes.NormalTeacher _
                            | Untis.DataTransferTypes.Custodian _ -> None
                        )
                }
            )
        return!
            Ok fn
            |> Result.apply (Result.mapError List.singleton untisTeachingData)
            |> Result.apply (Result.mapError List.singleton sokratesTeachers)
            |> function
            | Ok v -> Successful.OK v next ctx
            | Error e -> ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/ad/updates" >=> Auth.requiresAdmin >=> ADModifications.HttpHandler.getADModifications
                    route "/aad/group-updates" >=> Auth.requiresAdmin >=> getAADGroupUpdates
                    route "/consultation-hours" >=> getConsultationHours
                ]
                POST >=> choose [
                    route "/ad/updates/apply" >=> Auth.requiresAdmin >=> ADModifications.HttpHandler.applyADModifications
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
        |> Extra.withCustom ADModifications.DataTransferTypes.DirectoryModification.encode ADModifications.DataTransferTypes.DirectoryModification.decoder
        |> Extra.withCustom Shared.AADGroupUpdates.GroupUpdate.encode Shared.AADGroupUpdates.GroupUpdate.decoder
    services.AddSingleton<IJsonSerializer>(ThothSerializer(isCamelCase = true, extra = coders)) |> ignore

let configureLogging (ctx: HostBuilderContext) (builder : ILoggingBuilder) =
    builder
        .AddFilter(fun l -> ctx.HostingEnvironment.IsDevelopment() || l.Equals LogLevel.Error)
        .AddConsole()
        .AddDebug()
    |> ignore

[<EntryPoint>]
let main args =
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHostBuilder -> webHostBuilder.Configure configureApp |> ignore)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
