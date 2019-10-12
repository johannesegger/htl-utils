module App

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.Graph
open Microsoft.Graph.Auth
open Microsoft.Identity.Client
open Shared
open System
open Thoth.Json.Giraffe
open Thoth.Json.Net

let private clientApp =
    ConfidentialClientApplicationBuilder
        .Create(Environment.getEnvVarOrFail "MICROSOFT_GRAPH_CLIENT_ID")
        .WithTenantId(Environment.getEnvVarOrFail "MICROSOFT_GRAPH_TENANT_ID")
        .WithClientSecret(Environment.getEnvVarOrFail "MICROSOFT_GRAPH_APP_KEY")
        .Build()

let getGraphServiceClient () =
    ClientCredentialProvider(clientApp)
    |> GraphServiceClient

// ---------------------------------
// Web app
// ---------------------------------

let handleGetAutoGroups : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) -> task {
        let graphServiceClient = getGraphServiceClient ()

        let! aadGroups =
            AAD.getAutoGroups graphServiceClient
            |> Async.map List.toArray
        return! Successful.OK aadGroups next ctx
    }

// let handlePostGroupUpdates : HttpHandler =
//     fun (next : HttpFunc) (ctx : HttpContext) -> task {
//         return! Successful.OK () next ctx
//     }

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/auto-groups" >=> handleGetAutoGroups
                ]
                // POST >=> choose [
                //     route "/group-updates" >=> handlePostGroupUpdates
                // ]
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
        |> Extra.withCustom Group.encode Group.decoder
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