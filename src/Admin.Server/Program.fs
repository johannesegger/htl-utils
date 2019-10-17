module App

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System
open Thoth.Json.Giraffe
open Thoth.Json.Net
open System.Net.Http

let httpGet (httpClientFactory: IHttpClientFactory) (url: string) = async {
    use httpClient = httpClientFactory.CreateClient()
    let! response = httpClient.GetAsync url |> Async.AwaitTask
    response.EnsureSuccessStatusCode() |> ignore
    let! responseContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
    // TODO deserialize
}

// ---------------------------------
// Web app
// ---------------------------------

let handleGetAutoGroups : HttpHandler =
    fun next ctx -> Successful.OK () next ctx

let requiresUser preferredUsername : HttpHandler =
    authorizeUser
        (fun user -> user.HasClaim("preferred_username", preferredUsername))
        (RequestErrors.forbidden (setBody [||]))

let requiresAdmin : HttpHandler = requiresUser "admin@htlvb.at"

let getAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let httpClientFactory = ctx.GetService<IHttpClientFactory>()
        let! teachingData = httpGet httpClientFactory "http://untis/api/teaching-data" |> Async.StartChild
        let! sokratesTeachers = httpGet httpClientFactory "http://sokrates/api/teachers" |> Async.StartChild
        // let! finalThesesMentors = httpGet httpClientFactory "http://final-theses/api/mentors" |> Async.StartChild
        let! aadGroups = httpGet httpClientFactory "http://aad/api/groups" |> Async.StartChild
        let! aadUsers = httpGet httpClientFactory "http://aad/api/users" |> Async.StartChild
        return! Successful.OK () next ctx
    }

let applyAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        return! Successful.OK () next ctx
    }

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/aad/group-updates" >=> requiresAdmin >=> getAADGroupUpdates
                ]
                POST >=> choose [
                    route "/aad/group-updates/apply" >=> requiresAdmin >=> applyAADGroupUpdates
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
        // |> Extra.withCustom Group.encode Group.decoder
        // |> Extra.withCustom User.encode User.decoder
    services.AddSingleton<IJsonSerializer>(ThothSerializer(isCamelCase = true, extra = coders)) |> ignore
    services
        .AddAuthentication(fun config ->
            config.DefaultScheme <- JwtBearerDefaults.AuthenticationScheme
            config.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(fun config ->
            config.Audience <- Environment.getEnvVarOrFail "MICROSOFT_GRAPH_CLIENT_ID"
            config.Authority <- Environment.getEnvVarOrFail "MICROSOFT_GRAPH_AUTHORITY"
            config.TokenValidationParameters.ValidateIssuer <- false
            config.TokenValidationParameters.SaveSigninToken <- true
        ) |> ignore

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