module PhotoLibrary.Server

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open PhotoLibrary.DataTransferTypes
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing
open SixLabors.Primitives
open System
open System.IO
open System.Text.RegularExpressions
open Thoth.Json.Giraffe
open Thoth.Json.Net

module TeacherPhoto =
    let tryGetShortName (path: string) =
        let fileName = Path.GetFileNameWithoutExtension path
        if Regex.IsMatch(fileName, @"^[A-Z]{4}$")
        then Some fileName
        else None

    let tryParse readFn file =
        tryGetShortName file
        |> Option.map (fun shortName ->
            {
                ShortName = shortName
                Data = readFn file |> Convert.ToBase64String |> Base64EncodedImage
            }
        )

module StudentPhoto =
    let tryGetStudentId (path: string) =
        let fileName = Path.GetFileNameWithoutExtension path
        if Regex.IsMatch(fileName, @"^\d+$")
        then Some (SokratesId fileName)
        else None

    let tryParse readFn file =
        tryGetStudentId file
        |> Option.map (fun studentId ->
            {
                StudentId = studentId
                Data = readFn file |> Convert.ToBase64String |> Base64EncodedImage
            }
        )

let resizePhoto size (path: string) =
    use image = Image.Load path
    image.Mutate(fun x ->
        let resizeOptions =
            ResizeOptions(
                Size = size,
                Mode = ResizeMode.Crop,
                CenterCoordinates = [ 0.f; 0.4f ]
            )
        x.Resize resizeOptions |> ignore
    )
    use target = new MemoryStream()
    image.SaveAsJpeg target
    target.Seek(0L, SeekOrigin.Begin) |> ignore
    target.ToArray()

let resize (ctx: HttpContext) =
    let width =
        tryDo ctx.Request.Query.TryGetValue "width"
        |> Option.bind Seq.tryHead
        |> Option.bind (tryDo Int32.TryParse)
    let height =
        tryDo ctx.Request.Query.TryGetValue "height"
        |> Option.bind Seq.tryHead
        |> Option.bind (tryDo Int32.TryParse)
    match width, height with
    | Some width, Some height -> resizePhoto (Size (width, height))
    | Some width, None -> resizePhoto (Size (width, 0))
    | None, Some height -> resizePhoto (Size (0, height))
    | None, None -> File.ReadAllBytes

// ---------------------------------
// Web app
// ---------------------------------

let handleGetTeachersWithPhotos : HttpHandler =
    let baseDir = Environment.getEnvVarOrFail "TEACHER_PHOTOS_DIRECTORY"
    fun next ctx ->
        let result =
            Directory.GetFiles baseDir
            |> Seq.choose TeacherPhoto.tryGetShortName
            |> Seq.toList
        Successful.OK result next ctx

let handleGetStudentsWithPhotos : HttpHandler =
    let baseDir = Environment.getEnvVarOrFail "STUDENT_PHOTOS_DIRECTORY"
    fun next ctx ->
        let result =
            Directory.GetFiles baseDir
            |> Seq.choose StudentPhoto.tryGetStudentId
            |> Seq.toList
        Successful.OK result next ctx

let private tryGetFile baseDir fileName =
    Directory.GetFiles(baseDir, sprintf "%s.*" fileName, EnumerationOptions(MatchCasing = MatchCasing.CaseInsensitive))
    |> Array.tryHead

let handleGetTeacherPhotos : HttpHandler =
    let baseDir = Environment.getEnvVarOrFail "TEACHER_PHOTOS_DIRECTORY"
    fun next ctx ->
        let result =
            Directory.GetFiles baseDir
            |> Seq.choose (TeacherPhoto.tryGetShortName >> Option.bind (tryGetFile baseDir) >> Option.bind (TeacherPhoto.tryParse (resize ctx)))
            |> Seq.toList
        Successful.OK result next ctx

let handleGetTeacherPhoto shortName : HttpHandler =
    let baseDir = Environment.getEnvVarOrFail "TEACHER_PHOTOS_DIRECTORY"
    fun next ctx ->
        match tryGetFile baseDir shortName |> Option.bind (TeacherPhoto.tryParse (resize ctx)) with
        | Some result ->
            let (Base64EncodedImage data) = result.Data
            let bytes = Convert.FromBase64String data
            Successful.ok (setBody bytes) next ctx
        | None -> RequestErrors.notFound (setBody [||]) next ctx

let handleGetStudentPhoto studentId : HttpHandler =
    let baseDir = Environment.getEnvVarOrFail "STUDENT_PHOTOS_DIRECTORY"
    fun next ctx ->
        match tryGetFile baseDir studentId |> Option.bind (StudentPhoto.tryParse (resize ctx)) with
        | Some result ->
            let (Base64EncodedImage data) = result.Data
            let bytes = Convert.FromBase64String data
            Successful.ok (setBody bytes) next ctx
        | None -> RequestErrors.notFound (setBody [||]) next ctx

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/teachers" >=> handleGetTeachersWithPhotos
                    route "/teachers/photos" >=> handleGetTeacherPhotos
                    routef "/teachers/%s/photo" handleGetTeacherPhoto
                    route "/students" >=> handleGetStudentsWithPhotos
                    routef "/students/%s/photo" handleGetStudentPhoto
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
        |> Extra.withCustom SokratesIdModule.encode (Decode.fail "Not implemented")
        |> Extra.withCustom TeacherPhoto.encode (Decode.fail "Not implemented")
        |> Extra.withCustom StudentPhoto.encode (Decode.fail "Not implemented")
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