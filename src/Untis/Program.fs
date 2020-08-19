module Untis.Server

open FSharp.Control.Tasks.V2.ContextInsensitive
open FSharp.Data
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open System
open System.IO
open Thoth.Json.Giraffe
open Thoth.Json.Net
open Untis.DataTransferTypes

[<Literal>]
let TimetablePath = __SOURCE_DIRECTORY__ + "/data/GPU001.TXT"
type Timetable = CsvProvider<TimetablePath, Schema=",Class,Teacher,Subject,Room,Day,Period", Separators="\t">

let timetable =
    Environment.getEnvVarOrFail "UNTIS_GPU001_FILE_PATH"
    |> File.ReadAllText
    |> Timetable.ParseRows

[<Literal>]
let TeachingDataPath = __SOURCE_DIRECTORY__ + "/data/GPU002.TXT"
type TeachingData = CsvProvider<TeachingDataPath, Schema=",,,,Class,Teacher,Subject", Separators="\t">

let teachingData =
    Environment.getEnvVarOrFail "UNTIS_GPU002_FILE_PATH"
    |> File.ReadAllText
    |> TeachingData.ParseRows

[<Literal>]
let RoomsPath = __SOURCE_DIRECTORY__ + "/data/GPU005.TXT"
type Rooms = CsvProvider<RoomsPath, Schema="ShortName,FullName">

let rooms =
    Environment.getEnvVarOrFail "UNTIS_GPU005_FILE_PATH"
    |> File.ReadAllText
    |> Rooms.ParseRows

[<Literal>]
let SubjectsPath = __SOURCE_DIRECTORY__ + "/data/GPU006.TXT"
type Subjects = CsvProvider<SubjectsPath, Schema="ShortName,FullName">

let subjects =
    Environment.getEnvVarOrFail "UNTIS_GPU006_FILE_PATH"
    |> File.ReadAllText
    |> Subjects.ParseRows

let private timeFrames =
    Environment.getEnvVarOrFail "UNTIS_TIME_FRAMES"
    |> fun s -> s.Split ';'
    |> Seq.map (fun t ->
        t.Split '-'
        |> Seq.choose (tryDo TimeSpan.TryParse)
        |> Seq.toList
        |> function
        | ``begin`` :: [ ``end`` ] -> { BeginTime = ``begin``; EndTime = ``end`` }
        | _ -> failwithf "Can't parse \"%s\" as time frame" t
    )
    |> Seq.toList

let tryGetTimeFrameFromPeriodNumber v =
    timeFrames
    |> List.tryItem (v - 1)

let getSubject shortName =
    subjects
    |> Seq.find (fun r -> CIString r.ShortName = CIString shortName)
    |> fun r -> { Subject.ShortName = r.ShortName; FullName = r.FullName }

// ---------------------------------
// Web app
// ---------------------------------

let handleGetTeachingData : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) -> task {
        let teacherTasks =
            teachingData
            |> Seq.choose (fun row ->
                if not <| String.IsNullOrEmpty row.Class && not <| String.IsNullOrEmpty row.Teacher && not <| String.IsNullOrEmpty row.Subject then
                    if CIString row.Subject = CIString "ord" then
                        FormTeacher (SchoolClass row.Class, TeacherShortName row.Teacher)
                        |> Some
                    else
                        NormalTeacher (SchoolClass row.Class, TeacherShortName row.Teacher, getSubject row.Subject)
                        |> Some
                elif CIString row.Subject = CIString "spr" then
                    timetable
                    |> Seq.filter (fun r -> CIString r.Teacher = CIString row.Teacher && CIString r.Subject = CIString "spr")
                    |> Seq.tryExactlyOne
                    |> Option.map (fun timetableEntry ->
                        let room =
                            rooms
                            |> Seq.find (fun r -> CIString r.ShortName = CIString timetableEntry.Room)
                            |> fun r -> { ShortName = r.ShortName; FullName = r.FullName }
                        Informant (TeacherShortName row.Teacher, room, WorkingDay.tryFromOrdinal timetableEntry.Day |> Option.get, tryGetTimeFrameFromPeriodNumber timetableEntry.Period |> Option.get)
                    )
                elif String.IsNullOrEmpty row.Class then
                    Custodian (TeacherShortName row.Teacher, getSubject row.Subject)
                    |> Some
                else
                    None
            )
            |> Seq.toArray
        return! Successful.OK teacherTasks next ctx
    }

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/teaching-data" >=> handleGetTeachingData
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
    services.AddGiraffe() |> ignore
    let coders =
        Extra.empty
        |> Extra.withCustom TeacherTask.encode (Decode.fail "Not implemented")
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