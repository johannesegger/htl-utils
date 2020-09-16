﻿module App

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

#if DEBUG
let authTest : HttpHandler =
    fun next ctx -> task {
        let! groups = async {
            let! groups = AAD.Auth.withAuthenticationFromHttpContext ctx AAD.Core.getUserGroups
            return
                groups
                |> Seq.map (function
                    | :? Microsoft.Graph.Group as group ->
                        sprintf "%s  * %s (Group, Id = %s)" Environment.NewLine group.DisplayName group.Id
                    | :? Microsoft.Graph.DirectoryRole as role ->
                        sprintf "%s  * %s (Directory role, Id = %s)" Environment.NewLine role.DisplayName role.Id
                    | other ->
                        sprintf "%s  * Unknown (Type = %s, Id = %s)" Environment.NewLine other.ODataType other.Id
                )
                |> String.concat ""
        }

        let claims =
            ctx.User.Claims
            |> Seq.map (fun claim -> sprintf "%s  * %s: %s" Environment.NewLine claim.Type claim.Value)
            |> String.concat ""

        let result =
            [
                sprintf "User: %O" ctx.User
                sprintf "User identity: %O" ctx.User.Identity
                sprintf "User identity name: %O" ctx.User.Identity.Name
                sprintf "User identity is authenticated: %O" ctx.User.Identity.IsAuthenticated
                sprintf "Claims: %s" claims
                sprintf "Groups: %s" groups
            ]
            |> String.concat Environment.NewLine
        return! Successful.ok (setBodyFromString result) next ctx
    }

let logRequest : HttpHandler =
    fun next ctx -> task {
        let! content = ctx.ReadBodyFromRequestAsync()
        printfn "Content: %s" content
        return! Successful.OK () next ctx
    }
#endif

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/ad/updates" >=> AAD.Auth.requiresAdmin >=> ADModifications.HttpHandler.getADModifications
                    route "/ad/increment-class-group-updates" >=> AAD.Auth.requiresAdmin >=> ADModifications.HttpHandler.getADIncrementClassGroupUpdates
                    route "/aad/group-updates" >=> AAD.Auth.requiresAdmin >=> AADGroupUpdates.HttpHandler.getAADGroupUpdates
                    route "/aad/increment-class-group-updates" >=> AAD.Auth.requiresAdmin >=> AADGroupUpdates.HttpHandler.getAADIncrementClassGroupUpdates
                    route "/consultation-hours" >=> ConsultationHours.HttpHandler.getConsultationHours
                    route "/computer-info" >=> ShowComputerInfo.HttpHandler.getComputerInfo
                    #if DEBUG
                    route "/auth-test" >=> authTest
                    #endif
                ]
                POST >=> choose [
                    route "/ad/updates/apply" >=> AAD.Auth.requiresAdmin >=> ADModifications.HttpHandler.applyADModifications
                    route "/ad/increment-class-group-updates/apply" >=> AAD.Auth.requiresAdmin >=> ADModifications.HttpHandler.applyADIncrementClassGroupUpdates
                    route "/aad/group-updates/apply" >=> AAD.Auth.requiresAdmin >=> AADGroupUpdates.HttpHandler.applyAADGroupUpdates
                    route "/aad/increment-class-group-updates/apply" >=> AAD.Auth.requiresAdmin >=> AADGroupUpdates.HttpHandler.applyAADIncrementClassGroupUpdates
                ]
            ])
        #if DEBUG
        route "/log" >=> logRequest
        #endif
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
    app
        .UseHttpsRedirection()
        .UseDefaultFiles()
        .UseStaticFiles()
        .UseAuthentication()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddHttpClient() |> ignore
    services.AddGiraffe() |> ignore
    let coders =
        Extra.empty
        |> Extra.withCustom ADModifications.DataTransferTypes.DirectoryModification.encode ADModifications.DataTransferTypes.DirectoryModification.decoder
        |> Extra.withCustom IncrementClassGroups.DataTransferTypes.ClassGroupModification.encode IncrementClassGroups.DataTransferTypes.ClassGroupModification.decoder
        |> Extra.withCustom AADGroupUpdates.DataTransferTypes.GroupUpdate.encode AADGroupUpdates.DataTransferTypes.GroupUpdate.decoder
        |> Extra.withCustom ConsultationHours.DataTransferTypes.ConsultationHourEntry.encode ConsultationHours.DataTransferTypes.ConsultationHourEntry.decoder
        |> Extra.withCustom ShowComputerInfo.DataTransferTypes.ComputerInfo.encode ShowComputerInfo.DataTransferTypes.ComputerInfo.decoder
    services.AddSingleton<IJsonSerializer>(ThothSerializer(isCamelCase = true, extra = coders)) |> ignore

    let clientId = Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_CLIENT_ID"
    let authority = Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_AUTHORITY"
    Server.addAADAuth services clientId authority

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
