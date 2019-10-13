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

let handleGetUsers : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) -> task {
        let graphServiceClient = getGraphServiceClient ()

        let! aadUsers =
            AAD.getUsers graphServiceClient
            |> Async.map List.toArray
        return! Successful.OK aadUsers next ctx
    }

let handlePostGroupsModifications : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) -> task {
        let graphServiceClient = getGraphServiceClient ()

        let! modifications = ctx.BindModelAsync()
        do! AAD.applyGroupsModifications graphServiceClient modifications
        return! Successful.OK () next ctx
    }

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/groups" >=> handleGetAutoGroups
                    route "/users" >=> handleGetUsers
                ]
                POST >=> choose [
                    route "/groups/modify" >=> handlePostGroupsModifications
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
        |> Extra.withCustom Group.encode Group.decoder
        |> Extra.withCustom User.encode User.decoder
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