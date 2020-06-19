module AAD.Server

open AAD.BusinessLogic
open AAD.DataTransferTypes
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Graph
open Microsoft.Graph.Auth
open Microsoft.Identity.Client
open System
open Thoth.Json.Giraffe
open Thoth.Json.Net

let private clientApp =
    ConfidentialClientApplicationBuilder
        .Create(Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_CLIENT_ID")
        .WithClientSecret(Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_APP_KEY")
        .WithRedirectUri("https://localhost:8080")
        .Build()

let authProvider = OnBehalfOfProvider(clientApp)

let graphServiceClient = GraphServiceClient(authProvider)

let getBearerToken (request: HttpRequest) =
    match request.Headers.TryGetValue "Authorization" with
    | (true, values) ->
        values
        |> Seq.tryPick (fun v ->
            if v.StartsWith("Bearer ", StringComparison.InvariantCultureIgnoreCase)
            then Some (v.Substring ("Bearer ".Length))
            else None
        )
        |> Option.defaultValue ""
    | _ -> ""

let acquireToken request scopes = async {
    let jwtToken = getBearerToken request
    let userAssertion = UserAssertion jwtToken
    do! clientApp.AcquireTokenOnBehalfOf(scopes, userAssertion).ExecuteAsync() |> Async.AwaitTask |> Async.Ignore
}
// ---------------------------------
// Web app
// ---------------------------------

let handleGetAutoGroups : HttpHandler =
    fun next ctx -> task {
        do! acquireToken ctx.Request [ "Group.ReadWrite.All" ]
        let! aadGroups =
            getAutoGroups graphServiceClient
            |> Async.map List.toArray
        return! Successful.OK aadGroups next ctx
    }

let handleGetUsers : HttpHandler =
    fun next ctx -> task {
        do! acquireToken ctx.Request [ "User.Read.All" ]
        let! aadUsers =
            getUsers graphServiceClient
            |> Async.map List.toArray
        return! Successful.OK aadUsers next ctx
    }

let handlePostGroupsModifications : HttpHandler =
    fun next ctx -> task {
        do! acquireToken ctx.Request [ "Group.ReadWrite.All" ]
        let! modifications = ctx.BindModelAsync()
        do! applyGroupsModifications graphServiceClient modifications
        return! Successful.OK () next ctx
    }

type Role = Admin | Teacher
module Role =
    let encode role =
        match role with
        | Admin -> Encode.string "admin"
        | Teacher -> Encode.string "teacher"

let handleGetSignedInUserRoles : HttpHandler =
    fun next ctx -> task {
        do! acquireToken ctx.Request [ "Directory.Read.All" ]
        let userId = ctx.User.ToGraphUserAccount().ObjectId
        let! groups = getUserGroups graphServiceClient (UserId userId)
        let groups =
            groups
            |> List.choose (function
                | :? DirectoryRole as role when CIString role.Id = CIString (Environment.getEnvVarOrFail "AAD_GLOBAL_ADMIN_ROLE_ID") -> Some Admin
                | :? Group as group when CIString group.Id = CIString (Environment.getEnvVarOrFail "AAD_TEACHER_GROUP_ID") -> Some Teacher
                | other -> None
            )
            |> List.distinct
        return! Successful.OK groups next ctx
    }

let handlePostAutoContacts : HttpHandler =
    fun next ctx -> task {
        do! acquireToken ctx.Request [ "Contacts.ReadWrite" ]
        let userId = ctx.User.ToGraphUserAccount().ObjectId
        let! contacts = ctx.BindModelAsync()
        updateAutoContacts graphServiceClient (UserId userId) contacts |> Async.Start
        return! Successful.ACCEPTED () next ctx
    }

#if DEBUG
let authTest : HttpHandler =
    fun next ctx -> task {
        let! groups = async {
            try
                do! acquireToken ctx.Request [ "Directory.Read.All" ]
                let graphUser = ctx.User.ToGraphUserAccount()
                let! groups = getUserGroups graphServiceClient (UserId graphUser.ObjectId)
                return
                    groups
                    |> Seq.map (function
                        | :? Group as group ->
                            sprintf "%s  * %s (Group, Id = %s)" Environment.NewLine group.DisplayName group.Id
                        | :? DirectoryRole as role ->
                            sprintf "%s  * %s (Directory role, Id = %s)" Environment.NewLine role.DisplayName role.Id
                        | other ->
                            sprintf "%s  * Unknown (Type = %s, Id = %s)" Environment.NewLine other.ODataType other.Id
                    )
                    |> String.concat ""
            with e ->
                return sprintf "%O" e
        }

        let result =
            [
                sprintf "User: %O" ctx.User
                sprintf "User identity: %O" ctx.User.Identity
                sprintf "User identity name: %O" ctx.User.Identity.Name
                sprintf "User identity is authenticated: %O" ctx.User.Identity.IsAuthenticated
                ctx.User.Claims
                |> Seq.map (fun claim -> sprintf "%s  * %s: %s" Environment.NewLine claim.Type claim.Value)
                |> String.concat ""
                |> sprintf "Claims: %s"
                groups
                |> sprintf "Groups: %s"
            ]
            |> String.concat Environment.NewLine
        return! Successful.ok (setBodyFromString result) next ctx
    }
#endif

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
#if DEBUG
                    route "/auth-test" >=> authTest
#endif
                    route "/auto-groups" >=> handleGetAutoGroups
                    route "/users" >=> handleGetUsers
                    route "/signed-in-user/roles" >=> handleGetSignedInUserRoles
                ]
                POST >=> choose [
                    route "/auto-groups/modify" >=> handlePostGroupsModifications
                    route "/auto-contacts" >=> handlePostAutoContacts
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
    app
        .UseAuthentication()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore
    let coders =
        Extra.empty
        |> Extra.withCustom Group.encode (Decode.fail "Not implemented")
        |> Extra.withCustom User.encode (Decode.fail "Not implemented")
        |> Extra.withCustom Role.encode (Decode.fail "Not implemented")
        |> Extra.withCustom (fun _ -> failwith "Not implemented") GroupModification.decoder
        |> Extra.withCustom (fun _ -> failwith "Not implemented") Contact.decoder
    services.AddSingleton<IJsonSerializer>(ThothSerializer(isCamelCase = true, extra = coders)) |> ignore
    services
        .AddAuthentication(fun config ->
            config.DefaultScheme <- JwtBearerDefaults.AuthenticationScheme
            config.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(fun config ->
            config.Audience <- Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_CLIENT_ID"
            config.Authority <- Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_AUTHORITY"
            config.TokenValidationParameters.ValidateIssuer <- false
            config.TokenValidationParameters.SaveSigninToken <- true
        ) |> ignore

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