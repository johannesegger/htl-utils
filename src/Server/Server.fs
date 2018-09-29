open System
open System.IO
open System.Text
open System.Net
open System.Net.Http.Headers
open System.Net.NetworkInformation
open System.Security.Claims
open FSharp.Control.Tasks.V2.ContextInsensitive
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Identity.Client
open Microsoft.Graph
open Giraffe
open Giraffe.Serialization
open Saturn
open Shared
open WakeUp
open Students
open StudentDirectories

let publicPath = Path.GetFullPath "../Client/public"
let port = 8085

let requiresUser preferredUsername : HttpHandler =
    evaluateUserPolicy
        (fun user ->
            user.HasClaim("preferred_username", preferredUsername)
        )
        (RequestErrors.FORBIDDEN "Accessing this API is not allowed")

let requiresGroup groupId : HttpHandler =
    evaluateUserPolicy
        (fun user ->
            user.HasClaim("groups", groupId)
        )
        (RequestErrors.FORBIDDEN "Accessing this API is not allowed")

let readStream (stream: Stream) = task {
    use reader = new StreamReader(stream, Encoding.UTF8)
    return! reader.ReadToEndAsync()
}

let configureSerialization (services:IServiceCollection) =
    let fableJsonSettings = Newtonsoft.Json.JsonSerializerSettings()
    fableJsonSettings.Converters.Add(Fable.JsonConverter())
    services.AddSingleton<IJsonSerializer>(NewtonsoftJsonSerializer fableJsonSettings)

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

let getGraphApiAccessToken (clientApp: ConfidentialClientApplication) (user: ClaimsPrincipal) scopes = async {
    let identity = Seq.head user.Identities
    let userAccessToken = identity.BootstrapContext :?> string
    let! graphApiAcquireTokenResult =
        clientApp.AcquireTokenOnBehalfOfAsync(scopes, UserAssertion userAccessToken)
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

let importTeacherContacts (clientApp: ConfidentialClientApplication) getTeachers : HttpHandler =
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
            | Ok list -> Successful.OK list next ctx
            | Error (GetClassListError message) ->
                ServerErrors.internalError (setBodyFromString (sprintf "Error while querying list of classes: %s" message)) next ctx
    }

let getStudentList students className : HttpHandler =
    fun next ctx -> task {
        let! result = students className |> Async.StartAsTask
        return!
            match result with
            | Ok list -> Successful.OK list next ctx
            | Error (Students.GetStudentsError message) ->
                ServerErrors.internalError (setBodyFromString (sprintf "Error while querying students from class \"%s\": %s" className message)) next ctx
    }

let getChildDirectories baseDirectories : HttpHandler =
    fun next ctx -> task {
        let! body = Controller.getJson<string list> ctx
        let response =
            match body with
            | []
            | [ "" ] ->
                printfn "Returning base directories"
                baseDirectories |> Map.toList |> List.map fst
            | baseDir :: children ->
                match Map.tryFind baseDir baseDirectories with
                | Some dir ->
                    let path = Path.Combine([| yield dir; yield! children |])
                    printfn "Returning child directories of %s" path
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

let createStudentDirectories baseDirectories getStudents : HttpHandler =
    fun next ctx -> task {
        let! input = Controller.getJson<CreateStudentDirectories.Input> ctx
        let baseDirectory = fst input.Path
        let! result =
            baseDirectories
            |> Map.tryFind baseDirectory
            |> Result.ofOption (InvalidBaseDirectory baseDirectory)
            |> Result.bindAsync (fun baseDirectory ->
                let path = Path.Combine([| yield baseDirectory; yield! snd input.Path |]) // TODO verify that absolute input.Path works as expected
                createStudentDirectories getStudents path input.ClassName
            )
        return!
            match result with
            | Ok _ -> Successful.OK () next ctx
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
    let createDirectoriesBaseDirectory =
        getEnvVarOrFail "CREATE_DIRECTORIES_BASE_DIRECTORIES"
        |> String.split ";"
        |> Seq.chunkBySize 2
        |> Seq.map (fun s -> s.[0], s.[1])
        |> Map.ofSeq
    let clientId = "9fb9b79b-6e66-4007-a94f-571d7e3b68c5"
    let clientApp =
        let authority = "https://login.microsoftonline.com/htlvb.at/"
        let redirectUri = "https://localhost:8080" // TODO adapt for production env?
        let clientCredential = ClientCredential(getEnvVarOrFail "APP_KEY")
        let userTokenCache = TokenCache()
        let appTokenCache = TokenCache()
        ConfidentialClientApplication(clientId, authority, redirectUri, clientCredential, userTokenCache, appTokenCache) 

    let requiresEggj : HttpHandler = requiresUser "EGGJ@htlvb.at"
    let requiresTeacher : HttpHandler = requiresGroup "2d1c8785-5350-4a3b-993c-62dc9bc30980"

    let webApp = router {
        post "/api/wakeup/send" (requiresEggj >=> sendWakeUpCommand)
        post "/api/teachers/import-contacts" (requiresTeacher >=> importTeacherContacts clientApp teachers)
        get "/api/classes" (getClassList classList)
        getf "/api/classes/%s/students" (getStudentList students)
        post "/api/create-student-directories/child-directories" (requiresEggj >=> getChildDirectories createDirectoriesBaseDirectory)
        post "/api/create-student-directories/create" (requiresEggj >=> createStudentDirectories createDirectoriesBaseDirectory students)
    }

    let app = application {
        use_router webApp
        memory_cache
        use_static publicPath
        service_config configureSerialization
        use_gzip
        use_jwt_authentication_with_config (fun options ->
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII <- true
            options.Audience <- clientId
            options.Authority <- "https://login.microsoftonline.com/htlvb.at/"
            options.TokenValidationParameters.ValidateIssuer <- false
            options.TokenValidationParameters.SaveSigninToken <- true
        )
        host_config(fun host ->
            host.UseKestrel(fun options ->
                options.Listen(IPAddress.Any, port, fun listenOptions ->
#if DEBUG
                    listenOptions.UseHttps() |> ignore
#else
                    listenOptions.UseHttps(sslCertPath, sslCertPassword) |> ignore
#endif
                )
            )
        )
        app_config(fun app ->
#if DEBUG
            app.UseDeveloperExceptionPage() |> ignore
#else
            // app.UseExceptionHandler("/Error")
            app.UseHsts()
#endif

            app.UseHttpsRedirection()
        )
    }
    run app
    0
