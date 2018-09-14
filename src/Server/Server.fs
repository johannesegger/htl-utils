open System
open System.IO
open System.Text
open FSharp.Control.Tasks.V2.ContextInsensitive
open System.Net.NetworkInformation
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Giraffe.Serialization
open Saturn
open Shared
open WakeUp
open ClassList
open StudentDirectories
open System.Security.Claims
open Saturn.ControllerHelpers
open Microsoft.Extensions.Primitives
open System.DirectoryServices.AccountManagement
open Novell.Directory.Ldap

let publicPath = Path.GetFullPath "../Client/public"
let port = 8085us

let ldapAuth username password =
    use connection = new LdapConnection()
    try
        connection.Connect("schulserver.schule.intern", 389)
        connection.Bind(username, password)
        let claims = [| Claim("name", username); Claim(ClaimTypes.Role, "Teacher") |]
        ClaimsIdentity(claims, "Basic") |> Ok
    with e -> Error e

let requiresUser username authFailedHandler : HttpHandler =
    fun next ctx ->
        let authHeader = ctx.Request.Headers.["Authorization"].ToString()
        if not <| isNull authHeader && authHeader.StartsWith("basic", StringComparison.OrdinalIgnoreCase)
        then
            let token = authHeader.Substring("Basic ".Length).Trim()
            let credentials =
                Convert.FromBase64String token
                |> Encoding.UTF8.GetString
                |> String.split ":"
            match ldapAuth credentials.[0] credentials.[1] with
            | Ok identity ->
                ctx.User <- ClaimsPrincipal(identity)
                next ctx
            | Error e ->
                printfn "LDAP auth failed: %O" e
                authFailedHandler next ctx
        else
            ctx.Response.StatusCode <- 401
            ctx.Response.Headers.["WWW-Authenticate"] <- StringValues("Basic")
            task { return Some ctx }

let requiresEggj: HttpHandler =
#if DEBUG
    fun next ctx -> next ctx
#else
    requiresUser "eggj" (RequestErrors.FORBIDDEN "Accessing this API is not allowed")
#endif

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

let getClassList classList : HttpHandler =
    fun next ctx -> task {
        let! result = getClassList classList |> Async.StartAsTask
        return!
            match result with
            | Ok list -> Successful.OK list next ctx
            | Error (GetClassListError message) ->
                ServerErrors.internalError (setBodyFromString (sprintf "Error while querying list of classes: %s" message)) next ctx
    }

let getChildDirectories baseDirectories : HttpHandler =
    fun next ctx -> task {
        let! body = Controller.getJson<string list> ctx
        let response =
            match body with
            | []
            | [ "" ] -> baseDirectories |> Map.toList |> List.map fst
            | baseDir :: children ->
                match Map.tryFind baseDir baseDirectories with
                | Some dir ->
                    try
                        Path.Combine([| yield dir; yield! children |])
                        |> Directory.GetDirectories
                        |> Seq.map Path.GetFileName
                        |> Seq.toList
                    with _ -> []
                | None -> []

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
            | Error (GetStudentsError (className, message)) ->
                ServerErrors.internalError (setBodyFromString (sprintf "Error while querying students from class \"%s\": %s" className message)) next ctx
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

[<EntryPoint>]
let main argv =
    let connectionString = Environment.GetEnvironmentVariable "SISDB_CONNECTION_STRING"
    let getClassListFromDb = Db.getClassList connectionString
    let getStudentsFromDb = Db.getStudents connectionString
    let createDirectoriesBaseDirectory =
        Environment.GetEnvironmentVariable "CREATE_DIRECTORIES_BASE_DIRECTORIES"
        |> String.split ";"
        |> Seq.chunkBySize 2
        |> Seq.map (fun s -> s.[0], s.[1])
        |> Map.ofSeq

    let webApp = router {
        post "/api/wakeup/send" (requiresEggj >=> sendWakeUpCommand)
        get "/api/students/classes" (getClassList getClassListFromDb)
        post "/api/create-student-directories/child-directories" (requiresEggj >=> getChildDirectories createDirectoriesBaseDirectory)
        post "/api/create-student-directories/create" (requiresEggj >=> createStudentDirectories createDirectoriesBaseDirectory getStudentsFromDb)
    }
    let app = application {
        url ("http://+:" + port.ToString() + "/")
        use_router webApp
        memory_cache
        use_static publicPath
        service_config configureSerialization
        use_gzip
    }
    run app
    0
