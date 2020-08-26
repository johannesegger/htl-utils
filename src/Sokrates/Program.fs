module Sokrates.App

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Giraffe.Serialization
open System
open System.Net.Http
open System.Security.Cryptography.X509Certificates
open Thoth.Json.Giraffe
open Thoth.Json.Net

// ---------------------------------
// Web app
// ---------------------------------

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/teachers" >=> HttpHandler.handleGetTeachers
                    route "/classes" >=> HttpHandler.handleGetClasses None
                    routef "/classes/%i" (Some >> HttpHandler.handleGetClasses)
                    routef "/classes/%s/students" (fun className -> HttpHandler.handleGetStudents (Some className) None)
                    routef "/classes/%s/students/%i-%i-%i" (fun (className, year, month, day) -> HttpHandler.handleGetStudents (Some className) (Some (DateTime(year, month, day))))
                    route "/students" >=> HttpHandler.handleGetStudents None None
                    routef "/students/%i-%i-%i" (fun (year, month, day) -> HttpHandler.handleGetStudents None (Some (DateTime(year, month, day))))
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
        |> Extra.withCustom DataTransferTypes.Teacher.encode DataTransferTypes.Teacher.decoder
        |> Extra.withCustom DataTransferTypes.Student.encode DataTransferTypes.Student.decoder
    services.AddSingleton<IJsonSerializer>(ThothSerializer(isCamelCase = true, extra = coders)) |> ignore
    services
        .AddHttpClient("SokratesApiClient", ignore)
        .ConfigurePrimaryHttpMessageHandler(fun () ->
            let clientCertFile = Environment.getEnvVarOrFail "SOKRATES_CLIENT_CERTIFICATE_PATH"
            let clientCertPassphrase = Environment.getEnvVarOrFail "SOKRATES_CLIENT_CERTIFICATE_PASSPHRASE"
            let httpClientHandler = new HttpClientHandler()
            let cert = new X509Certificate2(clientCertFile, clientCertPassphrase)
            httpClientHandler.ClientCertificates.Add(cert) |> ignore
            httpClientHandler :> HttpMessageHandler
        )
    |> ignore

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
