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
                ServerErrors.internalError (text (sprintf "%O" e)) next ctx
    }

let handleGetClassStudents schoolClass : HttpHandler =
    fun next ctx -> task {
        let decoder = Decode.list (Sokrates.Student.decoder |> Decode.map Sokrates.Student.toDto)
        let! result = Http.get ctx (sprintf "http://sokrates/api/classes/%s/students" schoolClass) decoder
        return!
            match result with
            | Ok list -> Successful.OK list next ctx
            | Error e ->
                ServerErrors.internalError (text (sprintf "%O" e)) next ctx
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
            let sokratesTeacherMap =
                sokratesTeachers
                |> List.map (fun (teacher: Sokrates.Teacher) -> CIString teacher.ShortName, teacher)
                |> Map.ofList
            let photoLibraryTeacherMap =
                teacherPhotos
                |> List.map (fun (photo: PhotoLibrary.TeacherPhoto) -> (CIString photo.LastName, CIString photo.FirstName), photo.Data)
                |> Map.ofList
            aadUsers
            |> List.choose (fun (aadUser: AAD.User) ->
                let sokratesTeacher = Map.tryFind (CIString aadUser.UserName) sokratesTeacherMap
                let photo = Map.tryFind (CIString aadUser.LastName, CIString aadUser.FirstName) photoLibraryTeacherMap
                match sokratesTeacher, photo with
                | None, None -> None
                | Some _, Some _
                | None, Some _
                | Some _, None ->
                    Some {
                        AAD.Contact.FirstName = aadUser.FirstName
                        AAD.Contact.LastName = aadUser.LastName
                        AAD.Contact.DisplayName = sprintf "%s %s (%s)" aadUser.LastName aadUser.FirstName aadUser.UserName
                        AAD.Contact.Birthday = sokratesTeacher |> Option.map (fun v -> v.DateOfBirth)
                        AAD.Contact.HomePhones =
                            sokratesTeacher
                            |> Option.map (fun v ->
                                v.Phones
                                |> List.choose (function
                                    | Sokrates.Home number -> Some number
                                    | Sokrates.Mobile _ -> None
                                )
                            )
                            |> Option.defaultValue []
                        AAD.Contact.MobilePhone =
                            sokratesTeacher
                            |> Option.bind (fun v ->
                                v.Phones
                                |> List.tryPick (function
                                    | Sokrates.Home _ -> None
                                    | Sokrates.Mobile number -> Some number
                                )
                            )
                        AAD.Contact.MailAddresses = aadUser.MailAddresses |> List.take 1
                        AAD.Contact.Photo = photo
                    }
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
                    // route "/child-directories" >=> Auth.requiresTeacher >=> getChildDirectories baseDirectories
                    // route "/directory-info" >=> Auth.requiresTeacher >=> getDirectoryInfo baseDirectories
                    // route "/create-student-directories" >=> Auth.requiresTeacher >=> createStudentDirectories baseDirectories students
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
        // |> Extra.withCustom Shared.AADGroupUpdates.GroupUpdate.encode Shared.AADGroupUpdates.GroupUpdate.decode
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