module App

open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
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
let private untisConfig = Untis.Configuration.Config.fromEnvironment ()

let private requiresAdmin = AAD.Auth.requiresAdmin

type SokratesConfig() =
    member val WebServiceUrl = "" with get, set
    member val UserName = "" with get, set
    member val Password = "" with get, set
    member val SchoolId = "" with get, set
    member val ClientCertificatePath = "" with get, set
    member val ClientCertificatePassphrase = "" with get, set
    member x.Build() : Sokrates.Config = {
        WebServiceUrl = x.WebServiceUrl
        UserName = x.UserName
        Password = x.Password
        SchoolId = x.SchoolId
        ClientCertificatePath = x.ClientCertificatePath
        ClientCertificatePassphrase = x.ClientCertificatePassphrase
    }

let webApp = fun next (ctx: HttpContext) ->
    let sokratesConfig = ctx.GetService<IOptions<SokratesConfig>>().Value.Build()
    let sokratesApi = Sokrates.SokratesApi(sokratesConfig)
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/ad/updates" >=> requiresAdmin >=> ADModifications.HttpHandler.getADModifications adConfig sokratesApi
                    route "/ad/increment-class-group-updates" >=> requiresAdmin >=> ADModifications.HttpHandler.getADIncrementClassGroupUpdates adConfig incrementClassGroupsConfig
                    route "/aad/group-updates" >=> requiresAdmin >=> AADGroupUpdates.HttpHandler.getAADGroupUpdates adConfig aadConfig finalThesesConfig untisConfig
                    route "/aad/increment-class-group-updates" >=> requiresAdmin >=> AADGroupUpdates.HttpHandler.getAADIncrementClassGroupUpdates aadConfig incrementClassGroupsConfig
                    route "/it-information/users" >=> requiresAdmin >=> GenerateITInformationSheet.HttpHandler.getUsers adConfig
                    route "/consultation-hours" >=> ConsultationHours.HttpHandler.getConsultationHours sokratesApi untisConfig
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
        setStatusCode 404 >=> text "Not Found" ] next ctx

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
    services.AddOptions<SokratesConfig>().BindConfiguration("Sokrates") |> ignore
    services.AddHttpClient() |> ignore
    services.AddGiraffe() |> ignore
    let coders =
        Extra.empty
        |> ADModifications.DataTransferTypes.Thoth.addCoders
        |> IncrementClassGroups.DataTransferTypes.Thoth.addCoders
        |> AADGroupUpdates.DataTransferTypes.Thoth.addCoders
        |> ConsultationHours.DataTransferTypes.Thoth.addCoders
        |> ComputerInfo.DataTransferTypes.Thoth.addCoders
        |> GenerateITInformationSheet.DataTransferTypes.Thoth.addCoders
    services.AddSingleton<Json.ISerializer>(ThothSerializer(extra = coders)) |> ignore

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
