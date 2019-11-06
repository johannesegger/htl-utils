module App

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
        |> String.replace ":" "-"
        |> PhysicalAddress.Parse
        |> Some
    with _e -> None

// ---------------------------------
// Web app
// ---------------------------------

let handleWakeUp macAddress : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) -> task {
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

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    match env.IsDevelopment() with
    | true -> app.UseDeveloperExceptionPage() |> ignore
    | false -> app.UseGiraffeErrorHandler errorHandler |> ignore
    app.UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore

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