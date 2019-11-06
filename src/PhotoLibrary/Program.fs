module App

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing
open SixLabors.Primitives
open System
open System.IO
open Thoth.Json.Giraffe
open Thoth.Json.Net

type Base64EncodedImage = Base64EncodedImage of string

type TeacherPhoto = {
    FirstName: string
    LastName: string
    Data: Base64EncodedImage
}
module TeacherPhoto =
    let encode v =
        let (Base64EncodedImage data) = v.Data
        Encode.object [
            "firstName", Encode.string v.FirstName
            "lastName", Encode.string v.LastName
            "data", Encode.string data
        ]
    let tryParse readFn (file: string) =
        let fileName = Path.GetFileNameWithoutExtension file
        match fileName.IndexOf '_' with
        | -1 -> None
        | separatorIndex ->
            let lastName = fileName.Substring(0, separatorIndex)
            let firstName = fileName.Substring(separatorIndex + 1)
            Some {
                LastName = lastName
                FirstName = firstName
                Data = readFn file |> Convert.ToBase64String |> Base64EncodedImage
            }

// ---------------------------------
// Web app
// ---------------------------------

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

let handleGetTeacherPhotos : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) -> task {
        let baseDir = Environment.getEnvVarOrFail "TEACHER_PHOTOS_DIRECTORY"
        let width =
            tryDo ctx.Request.Query.TryGetValue "width"
            |> Option.bind Seq.tryHead
            |> Option.bind (tryDo Int32.TryParse)
        let height =
            tryDo ctx.Request.Query.TryGetValue "height"
            |> Option.bind Seq.tryHead
            |> Option.bind (tryDo Int32.TryParse)
        let resize =
            match width, height with
            | Some width, Some height -> resizePhoto (Size (width, height))
            | Some width, None -> resizePhoto (Size (width, 0))
            | None, Some height -> resizePhoto (Size (0, height))
            | None, None -> File.ReadAllBytes
        let photos =
            Directory.GetFiles baseDir
            |> Seq.choose (TeacherPhoto.tryParse resize)
            |> Seq.toList
        return! Successful.OK photos next ctx
    }

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/teachers/photos" >=> handleGetTeacherPhotos
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
        |> Extra.withCustom TeacherPhoto.encode (Decode.fail "Not implemented")
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