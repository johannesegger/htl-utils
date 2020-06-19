module FinalTheses.Server

open FinalTheses.DataTransferTypes
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

[<Literal>]
let MentorsDataPath = __SOURCE_DIRECTORY__ + "/data/mentors.csv"
type Mentors = CsvProvider<MentorsDataPath, Separators = ";">

let mentors =
    Environment.getEnvVarOrFail "FINAL_THESES_MENTORS_FILE_PATH"
    |> File.ReadAllText
    |> Mentors.ParseRows

// ---------------------------------
// Web app
// ---------------------------------

let handleGetMentors : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) -> task {
        let mentors =
            mentors
            |> Seq.filter (fun row -> String.equalsCaseInsensitive row.Typ "Betreuer" && String.equalsCaseInsensitive row.Status "Aktiv")
            |> Seq.map (fun row -> { FirstName = row.Vorname.Trim(); LastName = row.Nachname.Trim(); MailAddress = row.Email.Trim() })
            |> Seq.toList
        return! Successful.OK mentors next ctx
    }

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/mentors" >=> handleGetMentors
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
        |> Extra.withCustom Mentor.encode (Decode.fail "Not implemented")
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