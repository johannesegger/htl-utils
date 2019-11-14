module FileStorage.Server

open FileStorage.DataTransferTypes
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open System
open System.IO
open Thoth.Json.Giraffe
open Thoth.Json.Net

let private baseDirectories =
    Environment.getEnvVarOrFail "BASE_DIRECTORIES"
    |> String.split ";"
    |> Seq.chunkBySize 2
    |> Seq.map (fun s -> s.[0], s.[1])
    |> Map.ofSeq

let virtualPathToRealPath (v: string) =
    let path = v.Split('\\', '/') |> List.ofArray
    match path with
    | [] | [""] -> Error EmptyPath
    | baseDirectory :: pathTail ->
        match baseDirectories |> Map.tryFind baseDirectory with
        | Some baseDirectory ->
            Path.Combine([| baseDirectory; yield! pathTail |]) // TODO verify that absolute pathTail works as expected
            |> Ok
        | None -> Error (InvalidBaseDirectory baseDirectory)

let createStudentDirectories parentDirectory names = async {
    return
        names
        |> List.map (fun name ->
            let path = Path.Combine(parentDirectory, name)
            try
                Directory.CreateDirectory path |> ignore
                Ok path
            with e ->
                Error { DirectoryName = name; ErrorMessage = e.Message }
        )
        |> Result.sequence
        |> Result.mapError CreatingSomeDirectoriesFailed
}

let fileInfo path =
    let info = System.IO.FileInfo path
    {
        Name = info.Name
        Size = Bytes info.Length
        CreationTime = info.CreationTime
        LastAccessTime = info.LastAccessTime
        LastWriteTime = info.LastWriteTime
    }

let rec directoryInfo path =
    let childDirectories =
        path
        |> Directory.GetDirectories
        |> Seq.map directoryInfo
        |> Seq.toList
    let childFiles =
        path
        |> Directory.GetFiles
        |> Seq.map fileInfo
        |> Seq.toList
    {
        Name = Path.GetFileName path
        Directories = childDirectories
        Files = childFiles
    }

// ---------------------------------
// Web app
// ---------------------------------

let handleGetChildDirectories : HttpHandler =
    fun next ctx -> task {
        let! path = ctx.BindJsonAsync<string>()
        let result =
            match virtualPathToRealPath path with
            | Ok path ->
                try
                    path
                    |> Directory.GetDirectories
                    |> Seq.map Path.GetFileName
                    |> Seq.toList
                    |> Ok
                with
                    | :? DirectoryNotFoundException -> Ok []
                    | e -> Error (EnumeratingDirectoryFailed e.Message)
            | Error EmptyPath -> baseDirectories |> Map.toList |> List.map fst |> Ok
            | Error (InvalidBaseDirectory _ as e) -> Error (GetChildDirectoriesError.PathMappingFailed e)
        return!
            match result with
            | Ok v -> Successful.OK v next ctx
            | Error (GetChildDirectoriesError.PathMappingFailed EmptyPath as e)
            | Error (GetChildDirectoriesError.PathMappingFailed (InvalidBaseDirectory _) as e) ->
                RequestErrors.BAD_REQUEST e next ctx
            | Error (EnumeratingDirectoryFailed _ as e) ->
                ServerErrors.INTERNAL_ERROR e next ctx
    }

let handlePostExerciseDirectories : HttpHandler =
    fun next ctx -> task {
        let! input = ctx.BindJsonAsync<CreateDirectoriesData>()
        let! result =
            virtualPathToRealPath input.Path
            |> Result.mapError PathMappingFailed
            |> Result.bindAsync (fun path -> createStudentDirectories path input.Names)
        return!
            match result with
            | Ok _ -> Successful.OK () next ctx
            | Error (PathMappingFailed EmptyPath as e)
            | Error (PathMappingFailed (InvalidBaseDirectory _) as e) ->
                RequestErrors.BAD_REQUEST e next ctx
            | Error (CreatingSomeDirectoriesFailed _ as e) ->
                ServerErrors.INTERNAL_ERROR e next ctx
    }

let handleGetDirectoryInfo : HttpHandler =
    fun next ctx -> task {
        let! path = ctx.BindJsonAsync<string>()
        let result =
            virtualPathToRealPath path
            |> Result.mapError GetChildDirectoriesError.PathMappingFailed
            |> Result.bind (fun realPath ->
                try
                    let result = directoryInfo realPath
                    let dirName = Path.GetFileName path
                    Ok { result with Name = if dirName = String.Empty then path else dirName }
                with e -> Error (EnumeratingDirectoryFailed e.Message)
            )
        return!
            match result with
            | Ok directoryInfo -> Successful.OK directoryInfo next ctx
            | Error (GetChildDirectoriesError.PathMappingFailed EmptyPath as e)
            | Error (GetChildDirectoriesError.PathMappingFailed (InvalidBaseDirectory _) as e) ->
                RequestErrors.BAD_REQUEST e next ctx
            | Error (EnumeratingDirectoryFailed _ as e) ->
                ServerErrors.INTERNAL_ERROR e next ctx
    }

let webApp =
    choose [
        subRoute "/api"
            (choose [
                POST >=> choose [
                    route "/child-directories" >=> handleGetChildDirectories
                    route "/exercise-directories" >=> handlePostExerciseDirectories
                    route "/directory-info" >=> handleGetDirectoryInfo
                ]
            ])
        setStatusCode 404 >=> text "Not Found"
    ]

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
        |> Extra.withCustom (fun _ -> failwith "Not implemented") CreateDirectoriesData.decoder
        |> Extra.withCustom PathMappingError.encode (Decode.fail "Not implemented")
        |> Extra.withCustom GetChildDirectoriesError.encode (Decode.fail "Not implemented")
        |> Extra.withCustom CreateStudentDirectoriesError.encode (Decode.fail "Not implemented")
        |> Extra.withCustom DirectoryInfo.encode (Decode.fail "Not implemented")
    services.AddSingleton<IJsonSerializer>(ThothSerializer(isCamelCase = true, extra = coders)) |> ignore

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