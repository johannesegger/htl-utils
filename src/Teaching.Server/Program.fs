module App

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System
open System.Net
open Thoth.Json.Giraffe
open Thoth.Json.Net

// ---------------------------------
// Web app
// ---------------------------------

let handleGetClasses : HttpHandler =
    fun next ctx -> task {
        let! result = Http.get ctx "http://sokrates/api/classes" (Decode.list Decode.string)
        return!
            match result with
            | Ok list -> Successful.OK list next ctx
            | Error e ->
                ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let handleGetClassStudents schoolClass : HttpHandler =
    fun next ctx -> task {
        let decoder =
            Sokrates.Student.decoder |> Decode.map (fun s -> sprintf "%s %s" (s.LastName.ToUpper()) s.FirstName)
            |> Decode.list
            |> Decode.map List.sort
        let! result = Http.get ctx (sprintf "http://sokrates/api/classes/%s/students" schoolClass) decoder
        return!
            match result with
            | Ok list -> Successful.OK list next ctx
            | Error e ->
                ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let handlePostWakeUp macAddress : HttpHandler =
    fun next ctx -> task {
        let! result = Http.post ctx (sprintf "http://wake-up-computer/api/wake-up/%s" macAddress) Encode.nil (Decode.succeed ())
        match result with
        | Ok () -> return! Successful.OK () next ctx
        | Error (Http.HttpError (_, HttpStatusCode.BadRequest, content)) -> return! RequestErrors.BAD_REQUEST content next ctx
        | Error e -> return! ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let handleAddTeachersAsContacts : HttpHandler =
    fun next ctx -> task {
        let! aadUsers = Http.get ctx "http://aad/api/users" (Decode.list AAD.User.decoder) |> Async.StartChild
        let! sokratesTeachers = Http.get ctx "http://sokrates/api/teachers" (Decode.list Sokrates.Teacher.decoder) |> Async.StartChild
        let! teacherPhotos = Http.get ctx "http://photo-library/api/teachers/photos?width=200&height=200" (Decode.list PhotoLibrary.TeacherPhoto.decoder) |> Async.StartChild

        let! aadUsers = aadUsers
        let! sokratesTeachers = sokratesTeachers
        let! teacherPhotos = teacherPhotos

        let getContacts aadUsers sokratesTeachers teacherPhotos =
            let aadUserMap =
                aadUsers
                |> List.map (fun (user: AAD.User) -> CIString user.UserName, user)
                |> Map.ofList
            let photoLibraryTeacherMap =
                teacherPhotos
                |> List.map (fun (photo: PhotoLibrary.TeacherPhoto) -> (CIString photo.LastName, CIString photo.FirstName), photo.Data)
                |> Map.ofList
            sokratesTeachers
            |> List.choose (fun (sokratesTeacher: Sokrates.Teacher) ->
                let aadUser = Map.tryFind (CIString sokratesTeacher.ShortName) aadUserMap
                let photo = Map.tryFind (CIString sokratesTeacher.LastName, CIString sokratesTeacher.FirstName) photoLibraryTeacherMap
                match aadUser with
                | Some aadUser ->
                    Some {
                        AAD.Contact.FirstName = sokratesTeacher.FirstName
                        AAD.Contact.LastName = sokratesTeacher.LastName
                        AAD.Contact.DisplayName = sprintf "%s %s (%s)" sokratesTeacher.LastName sokratesTeacher.FirstName sokratesTeacher.ShortName
                        AAD.Contact.Birthday = Some sokratesTeacher.DateOfBirth
                        AAD.Contact.HomePhones =
                            sokratesTeacher.Phones
                            |> List.choose (function
                                | Sokrates.Home number -> Some number
                                | Sokrates.Mobile _ -> None
                            )
                        AAD.Contact.MobilePhone =
                            sokratesTeacher.Phones
                            |> List.tryPick (function
                                | Sokrates.Home _ -> None
                                | Sokrates.Mobile number -> Some number
                            )
                        AAD.Contact.MailAddresses = List.take 1 aadUser.MailAddresses
                        AAD.Contact.Photo = photo
                    }
                | None -> None
            )

        let contacts =
            Ok getContacts
            |> Result.apply (aadUsers |> Result.mapError List.singleton)
            |> Result.apply (sokratesTeachers |> Result.mapError List.singleton)
            |> Result.apply (teacherPhotos |> Result.mapError List.singleton)

        match contacts with
        | Ok contacts ->
            match! Http.post ctx "http://aad/api/auto-contacts" ((List.map AAD.Contact.encode >> Encode.list) contacts) (Decode.succeed ()) with
            | Ok () ->
                return! Successful.OK () next ctx
            | Error e ->
                return! ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
        | Error e ->
            return! ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let handleGetChildDirectories : HttpHandler =
    fun next ctx -> task {
        let! body = ctx.BindJsonAsync<string>()
        let! result = Http.post ctx "http://file-storage/api/child-directories" (Encode.string body) (Decode.list Decode.string)
        return!
            match result with
            | Ok v -> Successful.OK v next ctx
            | Error e -> ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let handlePostStudentDirectories : HttpHandler =
    fun next ctx -> task {
        let! body = ctx.BindJsonAsync<Shared.CreateStudentDirectories.CreateDirectoriesData>()
        let! students = Http.get ctx (sprintf "http://sokrates/api/classes/%s/students" body.ClassName) (Decode.list Sokrates.Student.decoder)
        let! result =
            students
            |> Result.bindAsync (fun students ->
                let data = {
                    FileStorage.Path = body.Path
                    FileStorage.Names =
                        students
                        |> List.map (fun student -> sprintf "%s_%s" student.LastName student.FirstName)
                }
                Http.post ctx "http://file-storage/api/exercise-directories" (FileStorage.CreateDirectoriesData.encode data) (Decode.succeed ())
            )
        return!
            match result with
            | Ok () -> Successful.OK () next ctx
            | Error e -> ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let handleGetDirectoryInfo : HttpHandler =
    fun next ctx -> task {
        let! body = ctx.BindJsonAsync<string>()
        let decoder =
            let path = body.Split('\\', '/') |> Seq.rev |> Seq.skip 1 |> Seq.rev |> Seq.toList
            FileStorage.DirectoryInfo.decoder
            |> Decode.map (FileStorage.DirectoryInfo.toDto path)
        let! result = Http.post ctx "http://file-storage/api/directory-info" (Encode.string body) decoder
        return!
            match result with
            | Ok v -> Successful.OK v next ctx
            | Error e -> ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/classes" >=> handleGetClasses
                    routef "/classes/%s/students" handleGetClassStudents
                ]
                POST >=> choose [
                    routef "/wake-up/%s" (fun macAddress -> Auth.requiresTeacher >=> handlePostWakeUp macAddress)
                    route "/teachers/add-as-contacts" >=> Auth.requiresTeacher >=> handleAddTeachersAsContacts
                    route "/child-directories" >=> Auth.requiresTeacher >=> handleGetChildDirectories
                    route "/create-student-directories" >=> Auth.requiresTeacher >=> handlePostStudentDirectories
                    route "/directory-info" >=> Auth.requiresTeacher >=> handleGetDirectoryInfo
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
    services.AddHttpClient() |> ignore
    services.AddGiraffe() |> ignore
    let coders =
        Extra.empty
        |> Extra.withCustom (fun _ -> failwith "Not implemented") Shared.CreateStudentDirectories.CreateDirectoriesData.decoder
        |> Extra.withCustom Shared.InspectDirectory.DirectoryInfo.encode (Decode.fail "Not implemented")
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