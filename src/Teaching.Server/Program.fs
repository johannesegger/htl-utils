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
        let! result = Http.get ctx (ServiceUrl.sokrates "classes") (Decode.list Decode.string)
        return!
            match result with
            | Ok list -> Successful.OK list next ctx
            | Error e ->
                ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let handleGetClassStudents schoolClass : HttpHandler =
    fun next ctx -> task {
        let decoder =
            Sokrates.DataTransferTypes.Student.decoder |> Decode.map (fun s -> sprintf "%s %s" (s.LastName.ToUpper()) s.FirstName1)
            |> Decode.list
            |> Decode.map List.sort
        let! result = Http.get ctx (ServiceUrl.sokrates (sprintf "classes/%s/students" schoolClass)) decoder
        return!
            match result with
            | Ok list -> Successful.OK list next ctx
            | Error e ->
                ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let handlePostWakeUp macAddress : HttpHandler =
    fun next ctx -> task {
        let! result = Http.post ctx (ServiceUrl.wakeUpComputer (sprintf "wake-up/%s" macAddress)) Encode.nil (Decode.succeed ())
        match result with
        | Ok () -> return! Successful.OK () next ctx
        | Error (Http.HttpError (_, HttpStatusCode.BadRequest, content)) -> return! RequestErrors.BAD_REQUEST content next ctx
        | Error e -> return! ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let handleAddTeachersAsContacts : HttpHandler =
    fun next ctx -> task {
        let! aadUsers = Http.get ctx (ServiceUrl.aad "users") (Decode.list AAD.DataTransferTypes.User.decoder) |> Async.StartChild
        let! sokratesTeachers = Http.get ctx (ServiceUrl.sokrates "teachers") (Decode.list Sokrates.DataTransferTypes.Teacher.decoder) |> Async.StartChild
        let! teacherPhotos = Http.get ctx (ServiceUrl.photoLibrary "teachers/photos?width=200&height=200") (Decode.list PhotoLibrary.DataTransferTypes.TeacherPhoto.decoder) |> Async.StartChild

        let! aadUsers = aadUsers
        let! sokratesTeachers = sokratesTeachers
        let! teacherPhotos = teacherPhotos

        let getContacts aadUsers sokratesTeachers teacherPhotos =
            let aadUserMap =
                aadUsers
                |> List.map (fun (user: AAD.DataTransferTypes.User) -> CIString user.UserName, user)
                |> Map.ofList
            let photoLibraryTeacherMap =
                teacherPhotos
                |> List.map (fun (photo: PhotoLibrary.DataTransferTypes.TeacherPhoto) -> CIString photo.ShortName, photo.Data)
                |> Map.ofList
            sokratesTeachers
            |> List.choose (fun (sokratesTeacher: Sokrates.DataTransferTypes.Teacher) ->
                let aadUser = Map.tryFind (CIString sokratesTeacher.ShortName) aadUserMap
                let photo = Map.tryFind (CIString sokratesTeacher.ShortName) photoLibraryTeacherMap
                match aadUser with
                | Some aadUser ->
                    Some {
                        AAD.DataTransferTypes.Contact.FirstName = sokratesTeacher.FirstName
                        AAD.DataTransferTypes.Contact.LastName = sokratesTeacher.LastName
                        AAD.DataTransferTypes.Contact.DisplayName =
                            sprintf "%s %s (%s)" sokratesTeacher.LastName sokratesTeacher.FirstName sokratesTeacher.ShortName
                        AAD.DataTransferTypes.Contact.Birthday = Some sokratesTeacher.DateOfBirth
                        AAD.DataTransferTypes.Contact.HomePhones =
                            sokratesTeacher.Phones
                            |> List.choose (function
                                | Sokrates.DataTransferTypes.Home number -> Some number
                                | Sokrates.DataTransferTypes.Mobile _ -> None
                            )
                        AAD.DataTransferTypes.Contact.MobilePhone =
                            sokratesTeacher.Phones
                            |> List.tryPick (function
                                | Sokrates.DataTransferTypes.Home _ -> None
                                | Sokrates.DataTransferTypes.Mobile number -> Some number
                            )
                        AAD.DataTransferTypes.Contact.MailAddresses = List.take 1 aadUser.MailAddresses
                        AAD.DataTransferTypes.Contact.Photo =
                            photo
                            |> Option.map (fun (PhotoLibrary.DataTransferTypes.Base64EncodedImage data) ->
                                AAD.DataTransferTypes.Base64EncodedImage data
                            )
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
            match! Http.post ctx (ServiceUrl.aad "auto-contacts") ((List.map AAD.DataTransferTypes.Contact.encode >> Encode.list) contacts) (Decode.succeed ()) with
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
        let! result = Http.post ctx (ServiceUrl.fileStorage "child-directories") (Encode.string body) (Decode.list Decode.string)
        return!
            match result with
            | Ok v -> Successful.OK v next ctx
            | Error e -> ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let handlePostStudentDirectories : HttpHandler =
    fun next ctx -> task {
        let! body = ctx.BindJsonAsync<Shared.CreateStudentDirectories.CreateDirectoriesData>()
        let! students = Http.get ctx (ServiceUrl.sokrates (sprintf "classes/%s/students" body.ClassName)) (Decode.list Sokrates.DataTransferTypes.Student.decoder)
        let! result =
            students
            |> Result.bindAsync (fun students ->
                let data = {
                    FileStorage.DataTransferTypes.CreateDirectoriesData.Path = body.Path
                    FileStorage.DataTransferTypes.CreateDirectoriesData.Names =
                        students
                        |> List.map (fun student -> sprintf "%s_%s" student.LastName student.FirstName1)
                }
                Http.post ctx (ServiceUrl.fileStorage "exercise-directories") (FileStorage.DataTransferTypes.CreateDirectoriesData.encode data) (Decode.succeed ())
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
            FileStorage.DataTransferTypes.DirectoryInfo.decoder
            |> Decode.map (FileStorageTypeMapping.DirectoryInfo.toDto path)
        let! result = Http.post ctx (ServiceUrl.fileStorage "directory-info") (Encode.string body) decoder
        return!
            match result with
            | Ok v -> Successful.OK v next ctx
            | Error e -> ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let handleGetKnowNameGroups : HttpHandler =
    fun next ctx -> task {
        let! result = Http.get ctx (ServiceUrl.sokrates "classes") (Decode.list Decode.string)
        return!
            match result with
            | Ok classNames ->
                let groups = [
                    Shared.KnowName.Teachers
                    yield! classNames |> List.map Shared.KnowName.Students
                ]
                Successful.OK groups next ctx
            | Error e ->
                ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let handleGetKnowNameTeachers : HttpHandler =
    fun next ctx -> task {
        let! teachers = Http.get ctx (ServiceUrl.sokrates "teachers") (Decode.list Sokrates.DataTransferTypes.Teacher.decoder) |> Async.StartChild
        let! teachersWithPhotos = Http.get ctx (ServiceUrl.photoLibrary "teachers") (Decode.list Decode.string) |> Async.StartChild

        let! teachers = teachers
        let! teachersWithPhotos = teachersWithPhotos

        let teachersWithPhoto teachers teachersWithPhotos =
            let teachersWithPhotos =
                teachersWithPhotos
                |> List.map CIString
                |> Set.ofList
            teachers
            |> List.map (fun (teacher: Sokrates.DataTransferTypes.Teacher) ->
                {
                    Shared.KnowName.Person.DisplayName = sprintf "%s - %s %s" teacher.ShortName (teacher.LastName.ToUpper()) teacher.FirstName
                    Shared.KnowName.Person.ImageUrl =
                        if Set.contains (CIString teacher.ShortName) teachersWithPhotos
                        then Some (sprintf "/api/know-name/teachers/%s/photo" teacher.ShortName)
                        else None
                }
            )

        let result =
            Ok teachersWithPhoto
            |> Result.apply (teachers |> Result.mapError List.singleton)
            |> Result.apply (teachersWithPhotos |> Result.mapError List.singleton)
        return!
            match result with
            | Ok result -> Successful.OK result next ctx
            | Error e -> ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let handleGetKnowNameTeacherPhoto shortName : HttpHandler =
    Http.proxy (ServiceUrl.photoLibrary (sprintf "teachers/%s/photo" shortName))

let handleGetKnowNameStudentsFromClass className : HttpHandler =
    fun next ctx -> task {
        let! students = Http.get ctx (ServiceUrl.sokrates (sprintf "classes/%s/students" className)) (Decode.list Sokrates.DataTransferTypes.Student.decoder) |> Async.StartChild
        let! studentsWithPhotos = Http.get ctx (ServiceUrl.photoLibrary "students") (Decode.list PhotoLibrary.DataTransferTypes.SokratesIdModule.decoder) |> Async.StartChild

        let! students = students
        let! studentsWithPhotos = studentsWithPhotos

        let studentsWithPhoto students studentsWithPhotos =
            let studentsWithPhotos =
                studentsWithPhotos
                |> List.map (fun (PhotoLibrary.DataTransferTypes.SokratesId studentId) -> Sokrates.DataTransferTypes.SokratesId studentId)
                |> Set.ofList
            students
            |> List.map (fun (student: Sokrates.DataTransferTypes.Student) ->
                {
                    Shared.KnowName.Person.DisplayName = sprintf "%s %s" (student.LastName.ToUpper()) student.FirstName1
                    Shared.KnowName.Person.ImageUrl =
                        if Set.contains student.Id studentsWithPhotos
                        then
                            let (Sokrates.DataTransferTypes.SokratesId studentId) = student.Id
                            Some (sprintf "/api/know-name/students/%s/photo" studentId)
                        else None
                }
            )

        let result =
            Ok studentsWithPhoto
            |> Result.apply (students |> Result.mapError List.singleton)
            |> Result.apply (studentsWithPhotos |> Result.mapError List.singleton)

        return!
            match result with
            | Ok result -> Successful.OK result next ctx
            | Error e -> ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let handleGetKnowNameStudentPhoto studentId : HttpHandler =
    Http.proxy (ServiceUrl.photoLibrary (sprintf "students/%s/photo" studentId))

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/classes" >=> handleGetClasses
                    routef "/classes/%s/students" (fun className -> Auth.requiresTeacher >=> handleGetClassStudents className)
                    route "/know-name/groups" >=> handleGetKnowNameGroups
                    route "/know-name/teachers" >=> Auth.requiresTeacher >=> handleGetKnowNameTeachers
                    routef "/know-name/teachers/%s/photo" handleGetKnowNameTeacherPhoto // Can't check authorization if image is loaded from HTML img tag
                    routef "/know-name/students/%s" (fun className -> Auth.requiresTeacher >=> handleGetKnowNameStudentsFromClass className)
                    routef "/know-name/students/%s/photo" handleGetKnowNameStudentPhoto // Can't check authorization if image is loaded from HTML img tag
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
    app
        .UseHttpsRedirection()
        .UseDefaultFiles()
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddHttpClient() |> ignore
    services.AddGiraffe() |> ignore
    let coders =
        Extra.empty
        |> Extra.withCustom (fun _ -> failwith "Not implemented") Shared.CreateStudentDirectories.CreateDirectoriesData.decoder
        |> Extra.withCustom Shared.InspectDirectory.DirectoryInfo.encode (Decode.fail "Not implemented")
        |> Extra.withCustom Shared.KnowName.Group.encode (Decode.fail "Not implemented")
        |> Extra.withCustom Shared.KnowName.Person.encode (Decode.fail "Not implemented")
    services.AddSingleton<IJsonSerializer>(ThothSerializer(isCamelCase = true, extra = coders)) |> ignore

let configureLogging (ctx: HostBuilderContext) (builder : ILoggingBuilder) =
    builder
        .AddFilter(fun l -> ctx.HostingEnvironment.IsDevelopment() || l.Equals LogLevel.Error)
        .AddConsole()
        .AddDebug()
    |> ignore

[<EntryPoint>]
let main args =
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .Configure(configureApp)
                .UseWebRoot("../Teaching.Client")
            |> ignore
        )
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0