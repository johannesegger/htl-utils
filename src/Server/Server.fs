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

let publicPath = Path.GetFullPath "../Client/public"
let port = 8085us

let requiresUser username authFailedHandler =
    evaluateUserPolicy
        (fun user -> user.HasClaim(ClaimTypes.Name, username))
        authFailedHandler

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
        // use_httpsys_windows_auth false
    }
    run app
    0
