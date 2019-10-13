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

// ---------------------------------
// Web app
// ---------------------------------

let handleGetAutoGroups : HttpHandler =
    fun next ctx -> Successful.OK () next ctx

let requiresUser preferredUsername : HttpHandler =
    authorizeUser
        (fun user -> user.HasClaim("preferred_username", preferredUsername))
        (RequestErrors.FORBIDDEN "Accessing this API is not allowed")

let requiresAdmin : HttpHandler = requiresUser "admin@htlvb.at"

let getAADGroupUpdates clientApp : HttpHandler =
    fun next ctx -> task {
        let! aadGroups = AAD.getGrpGroups graphServiceClient
        let! aadUsers = AAD.getUsers graphServiceClient
        let! teachingData = task {
            use stream = ctx.Request.Form.Files.["untis-teaching-data"].OpenReadStream()
            use reader = new StreamReader(stream)
            let! content = reader.ReadToEndAsync()
            return Untis.TeachingData.ParseRows content
        }
        let classesWithTeachers = Untis.getClassesWithTeachers teachingData
        let classTeachers = Untis.getClassTeachers teachingData
        let! allTeachers = task {
            use stream = ctx.Request.Form.Files.["sokrates-teachers"].OpenReadStream()
            return! Sokrates.getTeachers stream
        }
        let! finalThesesMentors = task {
            use stream = ctx.Request.Form.Files.["final-theses-mentors"].OpenReadStream()
            use reader = new StreamReader(stream, Encoding.GetEncoding 1252)
            let! content = reader.ReadToEndAsync()
            return
                FinalTheses.Mentors.ParseRows content
                |> FinalTheses.getMentors
        }
        let groupUpdates =
            let groups =
                aadGroups
                |> List.map (fun g -> (g.Id, { Group.Id = g.Id; Name = g.Name }))
                |> Map.ofList
            let users =
                aadUsers
                |> List.map (fun u -> (u.Id, { User.Id = u.Id; ShortName = u.ShortName; FirstName = u.FirstName; LastName = u.LastName }))
                |> Map.ofList
            AADGroups.getGroupUpdates aadGroups aadUsers classesWithTeachers classTeachers allTeachers finalThesesMentors
            |> List.map (AADGroups.GroupUpdate.toDto users groups)
        return! Successful.OK groupUpdates next ctx
    }

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/api/aad/group-updates" >=> requiresAdmin >=> getAADGroupUpdates clientApp
                ]
                POST >=> choose [
                    route "/api/aad/group-updates/apply" >=> requiresAdmin >=> applyAADGroupUpdates clientApp
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
        // |> Extra.withCustom Group.encode Group.decoder
        // |> Extra.withCustom User.encode User.decoder
    services.AddSingleton<IJsonSerializer>(ThothSerializer(isCamelCase = true, extra = coders)) |> ignore
    services
        .AddAuthentication(fun config ->
            config.DefaultScheme <- JwtBearerDefaults.AuthenticationScheme
            config.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(fun config ->
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII <- true
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