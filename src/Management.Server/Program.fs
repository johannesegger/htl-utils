module App

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System
open System.Collections.Generic
open Thoth.Json.Giraffe
open Thoth.Json.Net

// ---------------------------------
// Web app
// ---------------------------------

#if DEBUG
let authTest : HttpHandler =
    fun next ctx -> task {
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
let private generateItInformationSheetConfig = GenerateITInformationSheet.Configuration.Config.fromEnvironment ()
let private incrementClassGroupsConfig = IncrementClassGroups.Configuration.Config.fromEnvironment ()
let private sokratesConfig = Sokrates.Configuration.Config.fromEnvironment ()
let private untisConfig = Untis.Configuration.Config.fromEnvironment ()

let private requiresAdmin = AAD.Auth.requiresAdmin

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/ad/updates" >=> requiresAdmin >=> ADModifications.HttpHandler.getADModifications adConfig sokratesConfig
                    route "/ad/increment-class-group-updates" >=> requiresAdmin >=> ADModifications.HttpHandler.getADIncrementClassGroupUpdates adConfig incrementClassGroupsConfig
                    route "/aad/group-updates" >=> requiresAdmin >=> AADGroupUpdates.HttpHandler.getAADGroupUpdates adConfig aadConfig finalThesesConfig untisConfig
                    route "/aad/increment-class-group-updates" >=> requiresAdmin >=> AADGroupUpdates.HttpHandler.getAADIncrementClassGroupUpdates aadConfig incrementClassGroupsConfig
                    route "/it-information/users" >=> requiresAdmin >=> GenerateITInformationSheet.HttpHandler.getUsers adConfig
                    route "/consultation-hours" >=> ConsultationHours.HttpHandler.getConsultationHours sokratesConfig untisConfig
                    route "/computer-info" >=> ComputerInfo.HttpHandler.getComputerInfo dataStoreConfig
                    #if DEBUG
                    route "/auth-test" >=> authTest
                    #endif
                ]
                POST >=> choose [
                    route "/ad/updates/apply" >=> requiresAdmin >=> ADModifications.HttpHandler.applyADModifications adConfig
                    route "/ad/increment-class-group-updates/apply" >=> requiresAdmin >=> ADModifications.HttpHandler.applyADIncrementClassGroupUpdates adConfig
                    route "/aad/group-updates/apply" >=> requiresAdmin >=> AADGroupUpdates.HttpHandler.applyAADGroupUpdates
                    route "/aad/increment-class-group-updates/apply" >=> requiresAdmin >=> AADGroupUpdates.HttpHandler.applyAADIncrementClassGroupUpdates aadConfig
                    route "/it-information/generate-sheet" >=> requiresAdmin >=> GenerateITInformationSheet.HttpHandler.generateSheet adConfig generateItInformationSheetConfig
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
    | true ->
        Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII <- true
        app.UseDeveloperExceptionPage() |> ignore
    | false -> app.UseGiraffeErrorHandler errorHandler |> ignore
    app
        .UseHttpsRedirection()
        .UseDefaultFiles()
        .UseStaticFiles()
        .UseAuthentication()
        .UseGiraffe(webApp)

let configureServices (hostBuilderContext: HostBuilderContext) (services : IServiceCollection) =
    services.AddHttpClient() |> ignore
    services.AddGiraffe() |> ignore
    let coders =
        Extra.empty
        |> Extra.withCustom ADModifications.DataTransferTypes.DirectoryModification.encode ADModifications.DataTransferTypes.DirectoryModification.decoder
        |> Extra.withCustom IncrementClassGroups.DataTransferTypes.ClassGroupModification.encode IncrementClassGroups.DataTransferTypes.ClassGroupModification.decoder
        |> Extra.withCustom AADGroupUpdates.DataTransferTypes.GroupUpdate.encode AADGroupUpdates.DataTransferTypes.GroupUpdate.decoder
        |> Extra.withCustom ConsultationHours.DataTransferTypes.ConsultationHourEntry.encode ConsultationHours.DataTransferTypes.ConsultationHourEntry.decoder
        |> Extra.withCustom ComputerInfo.DataTransferTypes.QueryResult.encode ComputerInfo.DataTransferTypes.QueryResult.decoder
        |> Extra.withCustom GenerateITInformationSheet.DataTransferTypes.User.encode GenerateITInformationSheet.DataTransferTypes.User.decoder
        |> Extra.withCustom GenerateITInformationSheet.DataTransferTypes.InformationSheet.encode GenerateITInformationSheet.DataTransferTypes.InformationSheet.decoder
    services.AddSingleton<IJsonSerializer>(ThothSerializer(isCamelCase = true, extra = coders)) |> ignore

    Server.addAADAuth services hostBuilderContext.Configuration

let configureLogging (ctx: HostBuilderContext) (builder : ILoggingBuilder) =
    builder
        .AddFilter(fun l -> ctx.HostingEnvironment.IsDevelopment() || l.Equals LogLevel.Error)
        .AddConsole()
        .AddDebug()
    |> ignore

[<EntryPoint>]
let main args =
    let configDict =
        [
            "AzureAd:Instance", aadConfig.OidcConfig.Instance
            "AzureAd:Domain", aadConfig.OidcConfig.Domain
            "AzureAd:TenantId", aadConfig.OidcConfig.TenantId
            "AzureAd:ClientId", aadConfig.OidcConfig.AppId
            "AzureAd:ClientSecret", aadConfig.OidcConfig.AppSecret
            "MicrosoftGraph:BaseUrl", "https://graph.microsoft.com/v1.0"
            "MicrosoftGraph:Scopes", ""
        ]
        |> Seq.map KeyValuePair
    Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration(fun hostBuilderContext config -> config.AddInMemoryCollection(configDict) |> ignore)
        .ConfigureWebHostDefaults(fun webHostBuilder -> webHostBuilder.Configure configureApp |> ignore)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
