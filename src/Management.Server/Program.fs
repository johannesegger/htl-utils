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

#if DEBUG
let authTest aadConfig : HttpHandler =
    fun next ctx -> task {
        let! groups = async {
            let! groups = AAD.Auth.withAuthenticationFromHttpContext ctx (fun graphClient userId -> AAD.Core.getUserGroups graphClient userId |> Reader.retn) |> Reader.run aadConfig
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

let private adConfig = AD.Configuration.Config.fromEnvironment ()
let private aadConfig = AAD.Configuration.Config.fromEnvironment ()
let private dataStoreConfig = DataStore.Configuration.Config.fromEnvironment ()
let private finalThesesConfig = FinalTheses.Configuration.Config.fromEnvironment ()
let private incrementClassGroupsConfig = IncrementClassGroups.Configuration.Config.fromEnvironment ()
let private sokratesConfig = Sokrates.Configuration.Config.fromEnvironment ()
let private untisConfig = Untis.Configuration.Config.fromEnvironment ()

let private requiresAdmin = AAD.Auth.requiresAdmin aadConfig

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/ad/updates" >=> requiresAdmin >=> ADModifications.HttpHandler.getADModifications adConfig sokratesConfig
                    route "/ad/increment-class-group-updates" >=> requiresAdmin >=> ADModifications.HttpHandler.getADIncrementClassGroupUpdates adConfig incrementClassGroupsConfig
                    route "/aad/group-updates" >=> requiresAdmin >=> AADGroupUpdates.HttpHandler.getAADGroupUpdates adConfig aadConfig finalThesesConfig untisConfig
                    route "/aad/increment-class-group-updates" >=> requiresAdmin >=> AADGroupUpdates.HttpHandler.getAADIncrementClassGroupUpdates aadConfig incrementClassGroupsConfig
                    route "/consultation-hours" >=> ConsultationHours.HttpHandler.getConsultationHours sokratesConfig untisConfig
                    route "/computer-info" >=> ComputerInfo.HttpHandler.getComputerInfo dataStoreConfig
                    #if DEBUG
                    route "/auth-test" >=> authTest aadConfig
                    #endif
                ]
                POST >=> choose [
                    route "/ad/updates/apply" >=> requiresAdmin >=> ADModifications.HttpHandler.applyADModifications adConfig
                    route "/ad/increment-class-group-updates/apply" >=> requiresAdmin >=> ADModifications.HttpHandler.applyADIncrementClassGroupUpdates adConfig
                    route "/aad/group-updates/apply" >=> requiresAdmin >=> AADGroupUpdates.HttpHandler.applyAADGroupUpdates aadConfig
                    route "/aad/increment-class-group-updates/apply" >=> requiresAdmin >=> AADGroupUpdates.HttpHandler.applyAADIncrementClassGroupUpdates aadConfig
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
        |> Extra.withCustom ComputerInfo.DataTransferTypes.QueryResult.encode ComputerInfo.DataTransferTypes.QueryResult.decoder
    services.AddSingleton<IJsonSerializer>(ThothSerializer(isCamelCase = true, extra = coders)) |> ignore

    Server.addAADAuth services aadConfig.GraphClientId aadConfig.GraphAuthority

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
