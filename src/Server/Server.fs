open System
open System.IO
open System.Net.Http.Headers
open System.Net.NetworkInformation
open System.Security.Claims
open FSharp.Control.Tasks.V2.ContextInsensitive
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Identity.Client
open Microsoft.Graph
open Giraffe
open Giraffe.Serialization
open Thoth.Json.Giraffe
open Thoth.Json.Net
open Shared
open Shared.CreateStudentDirectories
open Shared.InspectDirectory
open WakeUp
open Students
open StudentDirectories

let publicPath = Path.GetFullPath "../Client/public"

let requiresUser preferredUsername : HttpHandler =
    authorizeUser
        (fun user ->
            user.HasClaim("preferred_username", preferredUsername)
        )
        (RequestErrors.FORBIDDEN "Accessing this API is not allowed")

let requiresGroup groupId : HttpHandler =
    authorizeUser
        (fun user ->
            user.HasClaim("groups", groupId)
        )
        (RequestErrors.FORBIDDEN "Accessing this API is not allowed")

let sendWakeUpCommand : HttpHandler =
    fun next ctx -> task {
        let result = WakeUp.sendWakeUpCommand "pc-eggj" (PhysicalAddress.Parse "20-CF-30-81-37-03")
        return!
            match result with
            | Ok () -> Successful.OK () next ctx
            | Error (HostResolutionError hostName) ->
                ServerErrors.internalError (text (sprintf "Error while resolving host name \"%s\"" hostName)) next ctx
            | Error (GetIpAddressFromHostNameError (hostName, addresses)) ->
                ServerErrors.internalError (text (sprintf "Error while getting IP address from host name \"%s\". Address candidates: %A" hostName addresses)) next ctx
            | Error (WakeOnLanError (ipAddress, physicalAddress)) ->
                ServerErrors.internalError (text (sprintf "Error while sending WoL magic packet to %O (MAC address %O)" ipAddress physicalAddress)) next ctx
    }

let getGraphApiAccessToken (clientApp: IConfidentialClientApplication) (user: ClaimsPrincipal) scopes = async {
    let identity = Seq.head user.Identities
    let userAccessToken = identity.BootstrapContext :?> string
    let! graphApiAcquireTokenResult =
        clientApp.AcquireTokenOnBehalfOf(scopes, UserAssertion userAccessToken).ExecuteAsync()
        |> Async.AwaitTask
    return graphApiAcquireTokenResult.AccessToken
}

let getGraphApiClient accessToken =
    DelegateAuthenticationProvider(
        fun requestMessage ->
            requestMessage.Headers.Authorization <- AuthenticationHeaderValue("Bearer", accessToken)
            System.Threading.Tasks.Task.CompletedTask
        )
    |> GraphServiceClient

let importTeacherContacts clientApp getTeachers : HttpHandler =
    fun next ctx -> task {
        let! teachers = getTeachers

        let! graphApiAccessToken = getGraphApiAccessToken clientApp ctx.User [| "contacts.readwrite" |]
        let graphApiClient = getGraphApiClient graphApiAccessToken

        do! Teachers.import graphApiClient teachers

        return! Successful.OK () next ctx
    }

let getClassList classList : HttpHandler =
    fun next ctx -> task {
        let! result = getClassList classList |> Async.StartAsTask
        return!
            match result with
            | Ok list -> json list next ctx
            | Error (GetClassListError message) ->
                ServerErrors.internalError (setBodyFromString (sprintf "Error while querying list of classes: %s" message)) next ctx
    }

let getStudentList students className : HttpHandler =
    fun next ctx -> task {
        let! result = students className |> Async.StartAsTask
        return!
            match result with
            | Ok list -> json list next ctx
            | Error (Students.GetStudentsError message) ->
                ServerErrors.internalError (setBodyFromString (sprintf "Error while querying students from class \"%s\": %s" className message)) next ctx
    }

let getChildDirectories baseDirectories : HttpHandler =
    fun next ctx -> task {
        let! body = ctx.BindJsonAsync<DirectoryPath>()
        let response =
            match DirectoryPath.getNormalized body with
            | [] -> baseDirectories |> Map.toList |> List.map fst
            | baseDir :: children ->
                match Map.tryFind baseDir baseDirectories with
                | Some dir ->
                    let path = Path.Combine([| yield dir; yield! children |])
                    try
                        path
                        |> Directory.GetDirectories
                        |> Seq.map Path.GetFileName
                        |> Seq.toList
                    with e ->
                        eprintfn "Couldn't get child directories: %O" e
                        []
                | None ->
                    eprintfn "Invalid base directory \"%s\"" baseDir
                    []

        return! json response next ctx
    }

let fileInfo getClientPath path =
    let info = System.IO.FileInfo path
    {
        Path = getClientPath info.FullName
        Size = Bytes info.Length
        CreationTime = info.CreationTime
        LastAccessTime = info.LastAccessTime
        LastWriteTime = info.LastWriteTime
    }

let rec directoryInfo getClientPath path =
    let childDirectories =
        path
        |> Directory.GetDirectories
        |> Seq.map (directoryInfo getClientPath)
        |> Seq.toList
    let childFiles =
        path
        |> Directory.GetFiles
        |> Seq.map (fileInfo getClientPath)
        |> Seq.toList
    {
        Path = getClientPath path
        Directories = childDirectories
        Files = childFiles
    }

let getDirectoryInfo baseDirectories : HttpHandler =
    fun next ctx -> task {
        let! body = ctx.BindJsonAsync<DirectoryPath>()
        let response =
            match DirectoryPath.getNormalized body with
            | [] -> None
            | baseDir :: children as fullPath ->
                match Map.tryFind baseDir baseDirectories with
                | Some dir ->
                    try
                        let serverPath = Path.Combine([| yield dir; yield! children |])
                        let fn (path: string) =
                            path.Substring(serverPath.Length)
                            |> String.split (sprintf "%c" Path.DirectorySeparatorChar)
                            |> Seq.filter (not << String.IsNullOrEmpty)
                            |> Seq.append fullPath
                            |> Seq.toList
                        directoryInfo fn serverPath
                        |> Some
                    with e ->
                        eprintfn "Couldn't get directory info: %O" e
                        None
                | None ->
                    eprintfn "Invalid base directory \"%s\"" baseDir
                    None

        return! json response next ctx
    }

let createStudentDirectories baseDirectories getStudents : HttpHandler =
    fun next ctx -> task {
        let! input = ctx.BindJsonAsync<CreateDirectoriesData>()
        let path = DirectoryPath.getNormalized input.Path
        let! result =
            match path with
            | [] -> async { return Result.Error EmptyPath }
            | baseDirectory :: pathTail ->
                baseDirectories
                |> Map.tryFind baseDirectory
                |> Result.ofOption (InvalidBaseDirectory baseDirectory)
                |> Result.bindAsync (fun baseDirectory ->
                    let path = Path.Combine([| yield baseDirectory; yield! pathTail |]) // TODO verify that absolute input.Path works as expected
                    createStudentDirectories getStudents path input.ClassName
                )
        return!
            match result with
            | Ok _ -> Successful.OK () next ctx
            | Error EmptyPath ->
                RequestErrors.BAD_REQUEST "No path provided" next ctx
            | Error (InvalidBaseDirectory name) ->
                RequestErrors.BAD_REQUEST (sprintf "Invalid base directory \"%s\"" name) next ctx
            | Error (GetStudentsError (Students.GetStudentsError message)) ->
                ServerErrors.internalError (setBodyFromString (sprintf "Error while querying students from class \"%s\": %s" input.ClassName message)) next ctx
            | Error (CreatingSomeDirectoriesFailed x) ->
                let notCreatedDirectories =
                    x.NotCreatedDirectories
                    |> List.map (fun x ->
                        sprintf "%s - %s" x.DirectoryName x.ErrorMessage
                    )
                let message =
                    [ yield sprintf "Error while creating some directories in \"%s\":" x.BaseDirectory
                      yield "== Created directories =="
                      yield! x.CreatedDirectories
                      yield "== Not created directories =="
                      yield! notCreatedDirectories ]
                    |> String.concat Environment.NewLine
                ServerErrors.internalError (setBodyFromString message) next ctx
    }

let getEnvVar name =
    Environment.GetEnvironmentVariable name

let getEnvVarOrFail name =
    let value = getEnvVar name
    if isNull value
    then failwithf "Environment variable \"%s\" not set" name
    else value

[<EntryPoint>]
let main argv =
    let sslCertPath = getEnvVar "SSL_CERT_PATH"
    let sslCertPassword = getEnvVar "SSL_CERT_PASSWORD"
    let connectionString = getEnvVarOrFail "SISDB_CONNECTION_STRING"
    let classList = Db.getClassList connectionString
    let dbStudents = Db.getStudents connectionString
    let students = Students.getStudents dbStudents
    let teachers =
        let dbTeachers = Db.getTeachers connectionString
        let dbContacts = Db.getContacts connectionString
        let teacherImageDir = getEnvVarOrFail "TEACHER_IMAGE_DIR"
        Teachers.mapDbTeachers teacherImageDir dbContacts dbTeachers
    let baseDirectories =
        getEnvVarOrFail "BASE_DIRECTORIES"
        |> String.split ";"
        |> Seq.chunkBySize 2
        |> Seq.map (fun s -> s.[0], s.[1])
        |> Map.ofSeq
    let clientId = "9fb9b79b-6e66-4007-a94f-571d7e3b68c5"
    let clientApp =
        ConfidentialClientApplicationBuilder.Create(clientId)
            .WithAuthority("https://login.microsoftonline.com/htlvb.at/")
            .WithRedirectUri("https://localhost:8080") // TODO adapt for production env?
            .WithClientSecret(getEnvVarOrFail "APP_KEY")
            .Build()

    let requiresEggj : HttpHandler = requiresUser "EGGJ@htlvb.at"
    let requiresTeacher : HttpHandler = requiresGroup "2d1c8785-5350-4a3b-993c-62dc9bc30980"

    let webApp = choose [
        GET >=> choose [
            route "/api/classes" >=> getClassList classList
            routef "/api/classes/%s/students" (getStudentList students)
        ]
        POST >=> choose [
            route "/api/wakeup/send" >=> requiresEggj >=> sendWakeUpCommand
            route "/api/teachers/import-contacts" >=>  requiresTeacher >=> importTeacherContacts clientApp teachers
            route "/api/child-directories" >=> requiresEggj >=> getChildDirectories baseDirectories
            route "/api/directory-info" >=> requiresEggj >=> getDirectoryInfo baseDirectories
            route "/api/create-student-directories" >=> requiresEggj >=> createStudentDirectories baseDirectories students
        ]
    ]

    let errorHandler (ex : Exception) (logger : ILogger) =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> setStatusCode 500 >=> text ex.Message

    let configureApp (app : IApplicationBuilder) =
        let env = app.ApplicationServices.GetService<IHostingEnvironment>()
        match env.IsDevelopment() with
        | true  -> app.UseDeveloperExceptionPage() |> ignore
        | false -> app.UseGiraffeErrorHandler errorHandler |> ignore
        app
            .UseHttpsRedirection()
            .UseDefaultFiles()
            .UseStaticFiles()
            .UseAuthentication()
            .UseGiraffe(webApp)

    let configureServices (services : IServiceCollection) =
        services.AddGiraffe() |> ignore
        let coders =
            Extra.empty
            |> Extra.withCustom DirectoryInfo.encode DirectoryInfo.decode
            |> Extra.withCustom DirectoryPath.encode DirectoryPath.decode
        services.AddSingleton<IJsonSerializer>(ThothSerializer(isCamelCase = true, extra = coders)) |> ignore
        services
            .AddAuthentication(fun config ->
                config.DefaultScheme <- JwtBearerDefaults.AuthenticationScheme
                config.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(fun config ->
                Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII <- true
                config.Audience <- clientId
                config.Authority <- "https://login.microsoftonline.com/htlvb.at/"
                config.TokenValidationParameters.ValidateIssuer <- false
                config.TokenValidationParameters.SaveSigninToken <- true
            ) |> ignore

    let configureLogging (ctx: WebHostBuilderContext) (builder : ILoggingBuilder) =
        builder
            .AddFilter(fun l -> ctx.HostingEnvironment.IsDevelopment() || l.Equals LogLevel.Error)
            .AddConsole()
            .AddDebug() |> ignore

    WebHostBuilder()
        .UseKestrel(fun options ->
            options.ListenAnyIP 5000
            options.ListenAnyIP(5001, fun listenOptions ->
                listenOptions.UseHttps(sslCertPath, sslCertPassword) |> ignore
            )
        )
        .UseWebRoot(publicPath)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
