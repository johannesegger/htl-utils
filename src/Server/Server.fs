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
open Novell.Directory.Ldap

let publicPath = Path.GetFullPath "../Client/public"
let port = 8085us

let private tryFn fn =
    try
        Ok (fn())
    with e -> Error e

let private searchResultsToList (searchResults: LdapSearchResults) =
    [
        while searchResults.hasMore() do
        match tryFn searchResults.next with
        | Ok e -> yield e
        | Error e -> printfn "Error: %O" e
    ]

let private ldapAuth host port dnTemplate username password =
    use connection = new LdapConnection()
    try
        connection.Connect(host, port)
        let dn = dnTemplate (sprintf "CN=%s,OU=Lehrer,OU=Teacher,OU=Automatisch,OU=Benutzer,OU=VirtualSchool,DC=schule,DC=intern" username)
        connection.Bind(dn, password)

        let claims = [| Claim(ClaimTypes.Name, username); Claim(ClaimTypes.Role, "Teacher") |]
        ClaimsIdentity(claims, "Basic") |> Ok
    with e -> Error e

let authenticateBasic : HttpHandler =
    RequestErrors.unauthorized "Basic" "HTLVB-EGGJ" (setBody [||])

let requiresUser (auth: string -> string -> Result<ClaimsIdentity, exn>) username : HttpHandler =
    fun next ctx ->
        let authHeader = ctx.Request.Headers.["Authorization"].ToString()
        if not <| isNull authHeader && authHeader.StartsWith("basic", StringComparison.OrdinalIgnoreCase)
        then
            let token = authHeader.Substring("Basic ".Length).Trim()
            let credentials =
                Convert.FromBase64String token
                |> Encoding.UTF8.GetString
                |> String.split ":"
            match auth credentials.[0] credentials.[1] with
            | Ok identity when identity.HasClaim(ClaimTypes.Name, username) ->
                ctx.User <- ClaimsPrincipal(identity)
                next ctx
            | Ok identity ->
                ctx.User <- ClaimsPrincipal(identity)
                RequestErrors.FORBIDDEN "Accessing this API it not allowed" next ctx
            | Error _ ->
                authenticateBasic next ctx
        else
            authenticateBasic next ctx

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

    let ldapHost = Environment.GetEnvironmentVariable "LDAP_HOST"
    let ldapPort = Environment.GetEnvironmentVariable "LDAP_PORT" |> int
    let ldapDnTemplate = fun (cn: string) -> System.String.Format(Environment.GetEnvironmentVariable "LDAP_DN_TEMPLATE", cn)
    let auth = ldapAuth ldapHost ldapPort ldapDnTemplate
    let requiresEggj : HttpHandler =
#if DEBUG
        fun next ctx -> next ctx
#else
        requiresUser auth "eggj"
#endif

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
