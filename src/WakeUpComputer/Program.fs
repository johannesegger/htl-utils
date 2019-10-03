module WakeUpComputer.App

open EasyWakeOnLan
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open System
open System.Net.NetworkInformation

let tryParsePhysicalAddress value =
    try
        String.toUpper value
        |> PhysicalAddress.Parse
        |> Some
    with _e -> None

// ---------------------------------
// Web app
// ---------------------------------

let handleWakeUp macAddress =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            match tryParsePhysicalAddress macAddress with
            | Some macAddress ->
                let wolClient = new EasyWakeOnLanClient()
                do! wolClient.WakeAsync(macAddress.ToString())
                return! Successful.ok (setBody [||]) next ctx
            | None -> return! RequestErrors.badRequest (setBodyFromString "Invalid MAC address") next ctx
        }

let webApp =
    choose [
        subRoute "/api"
            (choose [
                POST >=> choose [
                    routef "/wake-up/%s" handleWakeUp
                ]
            ])
        setStatusCode 404 >=> text "Not Found"
    ]

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

let configureLogging (builder : ILoggingBuilder) =
    builder
        .AddFilter(fun l -> l.Equals LogLevel.Error)
        .AddConsole()
        .AddDebug() |> ignore

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