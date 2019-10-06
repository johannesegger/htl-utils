module Untis.App

open FSharp.Control.Tasks.V2.ContextInsensitive
open FSharp.Data
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open System
open System.IO
open Thoth.Json.Giraffe
open Thoth.Json.Net

[<Literal>]
let TeachingDataPath = __SOURCE_DIRECTORY__ + "/data/GPU002.TXT"
type TeachingData = CsvProvider<TeachingDataPath, Schema=",,,,Class,Teacher,Subject">

let teachingData =
    Environment.getEnvVarOrFail "GPU002_FILE_PATH"
    |> File.ReadAllText
    |> TeachingData.ParseRows

type SchoolClass = SchoolClass of string
module SchoolClass =
    let decoder : Decoder<_> = Decode.string |> Decode.map SchoolClass
    let encode (SchoolClass v) = Encode.string v


type TeacherShortName = TeacherShortName of string
module TeacherShortName =
    let decoder : Decoder<_> = Decode.string |> Decode.map TeacherShortName
    let encode (TeacherShortName v) = Encode.string v

type Subject = Subject of string
module Subject =
    let decoder : Decoder<_> = Decode.string |> Decode.map Subject
    let encode (Subject v) = Encode.string v

type TeacherTask =
    | NormalTeacher of SchoolClass * TeacherShortName * Subject
    | FormTeacher of SchoolClass * TeacherShortName
    | Custodian of TeacherShortName * Subject

module TeacherTask =
    let normalTeacherDecoder =
        Decode.object (fun get ->
            let schoolClass = get.Required.Field "schoolClass" SchoolClass.decoder
            let teacher = get.Required.Field "teacher" TeacherShortName.decoder
            let subject = get.Required.Field "subject" Subject.decoder
            NormalTeacher (schoolClass, teacher, subject)
        )

    let formTeacherDecoder =
        Decode.object (fun get ->
            let schoolClass = get.Required.Field "schoolClass" SchoolClass.decoder
            let teacher = get.Required.Field "teacher" TeacherShortName.decoder
            FormTeacher (schoolClass, teacher)
        )

    let custodianDecoder =
        Decode.object (fun get ->
            let teacher = get.Required.Field "teacher" TeacherShortName.decoder
            let subject = get.Required.Field "subject" Subject.decoder
            Custodian (teacher, subject)
        )

    let decoder : Decoder<_> =
        Decode.oneOf [
            Decode.field "normalTeacher" normalTeacherDecoder
            Decode.field "formTeacher" formTeacherDecoder
            Decode.field "custodian" custodianDecoder
        ]

    let encode = function
        | NormalTeacher (schoolClass, teacher, subject) ->
            let fields = [
                "schoolClass", SchoolClass.encode schoolClass
                "teacher", TeacherShortName.encode teacher
                "subject", Subject.encode subject
            ]
            Encode.object [ "normalTeacher", Encode.object fields ]
        | FormTeacher (schoolClass, teacher) ->
            let fields = [
                "schoolClass", SchoolClass.encode schoolClass
                "teacher", TeacherShortName.encode teacher
            ]
            Encode.object [ "formTeacher", Encode.object fields ]
        | Custodian (teacher, subject) ->
            let fields = [
                "teacher", TeacherShortName.encode teacher
                "subject", Subject.encode subject
            ]
            Encode.object [ "custodian", Encode.object fields ]

// ---------------------------------
// Web app
// ---------------------------------

let handleGetTeachingData : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) -> task {
        let teacherTasks =
            teachingData
            |> Seq.choose (fun row ->
                if not <| String.IsNullOrEmpty row.Class && not <| String.IsNullOrEmpty row.Teacher && not <| String.IsNullOrEmpty row.Subject then
                    if String.equalsCaseInsensitive row.Subject "ord" then
                        FormTeacher (SchoolClass row.Class, TeacherShortName row.Teacher)
                        |> Some
                    else
                        NormalTeacher (SchoolClass row.Class, TeacherShortName row.Teacher, Subject row.Subject)
                        |> Some
                elif String.IsNullOrEmpty row.Class && not <| String.equalsCaseInsensitive row.Subject "spr" then
                    Custodian (TeacherShortName row.Teacher, Subject row.Subject)
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

let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins("http://localhost:8080")
        .AllowAnyMethod()
        .AllowAnyHeader()
    |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  -> app.UseDeveloperExceptionPage()
    | false -> app.UseGiraffeErrorHandler errorHandler)
        .UseHttpsRedirection()
        .UseCors(configureCors)
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore
    let coders =
        Extra.empty
        |> Extra.withCustom TeacherTask.encode TeacherTask.decoder
    services.AddSingleton<IJsonSerializer>(ThothSerializer(isCamelCase = true, extra = coders)) |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder
        .AddFilter(fun l -> l.Equals LogLevel.Error)
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