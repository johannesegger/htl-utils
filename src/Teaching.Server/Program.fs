module App

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
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
        let! list = Sokrates.Core.getClasses None
        return! Successful.OK list next ctx
    }

let handleGetClassStudents schoolClass : HttpHandler =
    fun next ctx -> task {
        let! students = Sokrates.Core.getStudents (Some schoolClass) None
        let names =
            students
            |> List.map (fun student -> sprintf "%s %s" (student.LastName.ToUpper()) student.FirstName1)
            |> List.sort
        return! Successful.OK names next ctx
    }

let handlePostWakeUp macAddress : HttpHandler =
    fun next ctx -> task {
        let! result = WakeUpComputer.Core.wakeUp macAddress
        match result with
        | Ok () -> return! Successful.OK () next ctx
        | Error (WakeUpComputer.Core.InvalidMacAddress message) -> return! RequestErrors.BAD_REQUEST (sprintf "Invalid MAC address: %s" message) next ctx
    }

let handleAddTeachersAsContacts : HttpHandler =
    fun next ctx -> task {
        let! aadUsers = AAD.Auth.withAuthTokenFromHttpContext ctx AAD.Core.getUsers |> Async.StartChild
        let! sokratesTeachers = Sokrates.Core.getTeachers () |> Async.StartChild
        let teacherPhotos = PhotoLibrary.Core.getTeacherPhotos (Some 200, Some 200)

        let! aadUsers = aadUsers
        let! sokratesTeachers = sokratesTeachers

        let contacts =
            let aadUserMap =
                aadUsers
                |> List.map (fun user -> CIString user.UserName, user)
                |> Map.ofList
            let photoLibraryTeacherMap =
                teacherPhotos
                |> List.map (fun photo -> CIString photo.ShortName, photo.Data)
                |> Map.ofList
            sokratesTeachers
            |> List.choose (fun sokratesTeacher ->
                let aadUser = Map.tryFind (CIString sokratesTeacher.ShortName) aadUserMap
                let photo = Map.tryFind (CIString sokratesTeacher.ShortName) photoLibraryTeacherMap
                match aadUser with
                | Some aadUser ->
                    Some {
                        AAD.Domain.Contact.FirstName = sokratesTeacher.FirstName
                        AAD.Domain.Contact.LastName = sokratesTeacher.LastName
                        AAD.Domain.Contact.DisplayName =
                            sprintf "%s %s (%s)" sokratesTeacher.LastName sokratesTeacher.FirstName sokratesTeacher.ShortName
                        AAD.Domain.Contact.Birthday = Some sokratesTeacher.DateOfBirth
                        AAD.Domain.Contact.HomePhones =
                            sokratesTeacher.Phones
                            |> List.choose (function
                                | Sokrates.Domain.Home number -> Some number
                                | Sokrates.Domain.Mobile _ -> None
                            )
                        AAD.Domain.Contact.MobilePhone =
                            sokratesTeacher.Phones
                            |> List.tryPick (function
                                | Sokrates.Domain.Home _ -> None
                                | Sokrates.Domain.Mobile number -> Some number
                            )
                        AAD.Domain.Contact.MailAddresses = List.take 1 aadUser.MailAddresses
                        AAD.Domain.Contact.Photo =
                            photo
                            |> Option.map (fun (PhotoLibrary.Domain.Base64EncodedImage data) ->
                                AAD.Domain.Base64EncodedImage data
                            )
                    }
                | None -> None
            )

        AAD.Auth.withAuthenticationFromHttpContext ctx (fun authToken userId -> AAD.Core.updateAutoContacts authToken userId contacts) |> Async.Start
        return! Successful.ACCEPTED () next ctx
    }

let handleGetChildDirectories : HttpHandler =
    fun next ctx -> task {
        let! path = ctx.BindJsonAsync<string>()
        match FileStorage.Core.getChildDirectories path with
        | Ok v -> return! Successful.OK v next ctx
        | Error (FileStorage.Domain.GetChildDirectoriesError.PathMappingFailed FileStorage.Domain.EmptyPath as e)
        | Error (FileStorage.Domain.GetChildDirectoriesError.PathMappingFailed (FileStorage.Domain.InvalidBaseDirectory _) as e) ->
            return! RequestErrors.BAD_REQUEST e next ctx
        | Error (FileStorage.Domain.GetChildDirectoriesError.EnumeratingDirectoryFailed _ as e) ->
            return! ServerErrors.INTERNAL_ERROR e next ctx
    }

let handlePostStudentDirectories : HttpHandler =
    fun next ctx -> task {
        let! body = ctx.BindJsonAsync<Shared.CreateStudentDirectories.CreateDirectoriesData>()
        let! students = Sokrates.Core.getStudents (Some body.ClassName) None
        let names =
            students
            |> List.map (fun student -> sprintf "%s_%s" student.LastName student.FirstName1)
        match FileStorage.Core.createExerciseDirectories body.Path names with
        | Ok _ -> return! Successful.OK () next ctx
        | Error (FileStorage.Domain.CreateStudentDirectoriesError.PathMappingFailed FileStorage.Domain.EmptyPath as e)
        | Error (FileStorage.Domain.CreateStudentDirectoriesError.PathMappingFailed (FileStorage.Domain.InvalidBaseDirectory _) as e) ->
            return! RequestErrors.BAD_REQUEST e next ctx
        | Error (FileStorage.Domain.CreatingSomeDirectoriesFailed _ as e) ->
            return! ServerErrors.INTERNAL_ERROR e next ctx
    }

let handleGetDirectoryInfo : HttpHandler =
    fun next ctx -> task {
        let! path = ctx.BindJsonAsync<string>()
        match FileStorage.Core.getDirectoryInfo path with
        | Ok directoryInfo ->
            // TODO mapping back from real to virtual path should be already done in `FileStorage.Core.getDirectoryInfo`
            let parentPath = path.Split('\\', '/') |> Seq.rev |> Seq.skip 1 |> Seq.rev |> Seq.toList
            let result = FileStorage.Mapping.DirectoryInfo.toDto parentPath directoryInfo
            return! Successful.OK result next ctx
        | Error (FileStorage.Domain.GetChildDirectoriesError.PathMappingFailed FileStorage.Domain.EmptyPath as e)
        | Error (FileStorage.Domain.GetChildDirectoriesError.PathMappingFailed (FileStorage.Domain.InvalidBaseDirectory _) as e) ->
            return! RequestErrors.BAD_REQUEST e next ctx
        | Error (FileStorage.Domain.EnumeratingDirectoryFailed _ as e) ->
            return! ServerErrors.INTERNAL_ERROR e next ctx
    }

let handleGetKnowNameGroups : HttpHandler =
    fun next ctx -> task {
        let! classNames = Sokrates.Core.getClasses None
        let groups = [
            Shared.KnowName.Teachers
            yield! classNames |> List.map Shared.KnowName.Students
        ]
        return! Successful.OK groups next ctx
    }

let handleGetKnowNameTeachers : HttpHandler =
    fun next ctx -> task {
        let! teachers = Sokrates.Core.getTeachers ()
        let teachersWithPhotos = PhotoLibrary.Core.getTeachersWithPhotos ()

        let teachersWithPhoto =
            let teachersWithPhotos =
                teachersWithPhotos
                |> List.map CIString
                |> Set.ofList
            teachers
            |> List.map (fun (teacher: Sokrates.Domain.Teacher) ->
                {
                    Shared.KnowName.Person.DisplayName = sprintf "%s - %s %s" teacher.ShortName (teacher.LastName.ToUpper()) teacher.FirstName
                    Shared.KnowName.Person.ImageUrl =
                        if Set.contains (CIString teacher.ShortName) teachersWithPhotos
                        then Some (sprintf "/api/know-name/teachers/%s/photo" teacher.ShortName)
                        else None
                }
            )

        return! Successful.OK teachersWithPhoto next ctx
    }

let private imageSizeFromRequest (request: HttpRequest) =
    let width =
        tryDo request.Query.TryGetValue "width"
        |> Option.bind Seq.tryHead
        |> Option.bind (tryDo Int32.TryParse)
    let height =
        tryDo request.Query.TryGetValue "height"
        |> Option.bind Seq.tryHead
        |> Option.bind (tryDo Int32.TryParse)
    (width, height)

let handleGetKnowNameTeacherPhoto shortName : HttpHandler =
    fun next ctx -> task {
        match PhotoLibrary.Core.tryGetTeacherPhoto shortName (imageSizeFromRequest ctx.Request) with
        | Some teacherPhoto ->
            let (PhotoLibrary.Domain.Base64EncodedImage data) = teacherPhoto.Data
            let bytes = Convert.FromBase64String data
            return! Successful.ok (setBody bytes) next ctx
        | None ->
            return! RequestErrors.notFound HttpHandler.nil next ctx
    }

let handleGetKnowNameStudentsFromClass className : HttpHandler =
    fun next ctx -> task {
        let! students = Sokrates.Core.getStudents (Some className) None
        let studentsWithPhotos = PhotoLibrary.Core.getStudentsWithPhotos ()

        let studentsWithPhoto =
            let studentsWithPhotos =
                studentsWithPhotos
                |> List.map (fun (PhotoLibrary.Domain.SokratesId studentId) -> Sokrates.Domain.SokratesId studentId)
                |> Set.ofList
            students
            |> List.map (fun student ->
                {
                    Shared.KnowName.Person.DisplayName = sprintf "%s %s" (student.LastName.ToUpper()) student.FirstName1
                    Shared.KnowName.Person.ImageUrl =
                        if Set.contains student.Id studentsWithPhotos
                        then
                            let (Sokrates.Domain.SokratesId studentId) = student.Id
                            Some (sprintf "/api/know-name/students/%s/photo" studentId)
                        else None
                }
            )

        return! Successful.OK studentsWithPhoto next ctx
    }

let handleGetKnowNameStudentPhoto studentId : HttpHandler =
    fun next ctx -> task {
        match PhotoLibrary.Core.tryGetStudentPhoto studentId (imageSizeFromRequest ctx.Request) with
        | Some studentPhoto ->
            let (PhotoLibrary.Domain.Base64EncodedImage data) = studentPhoto.Data
            let bytes = Convert.FromBase64String data
            return! Successful.ok (setBody bytes) next ctx
        | None ->
            return! RequestErrors.notFound HttpHandler.nil next ctx
    }

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/classes" >=> handleGetClasses
                    routef "/classes/%s/students" (fun className -> AAD.Auth.requiresTeacher >=> handleGetClassStudents className)
                    route "/know-name/groups" >=> handleGetKnowNameGroups
                    route "/know-name/teachers" >=> AAD.Auth.requiresTeacher >=> handleGetKnowNameTeachers
                    routef "/know-name/teachers/%s/photo" handleGetKnowNameTeacherPhoto // Can't check authorization if image is loaded from HTML img tag
                    routef "/know-name/students/%s" (fun className -> AAD.Auth.requiresTeacher >=> handleGetKnowNameStudentsFromClass className)
                    routef "/know-name/students/%s/photo" handleGetKnowNameStudentPhoto // Can't check authorization if image is loaded from HTML img tag
                ]
                POST >=> choose [
                    routef "/wake-up/%s" (fun macAddress -> AAD.Auth.requiresTeacher >=> handlePostWakeUp macAddress)
                    route "/teachers/add-as-contacts" >=> AAD.Auth.requiresTeacher >=> handleAddTeachersAsContacts
                    route "/child-directories" >=> AAD.Auth.requiresTeacher >=> handleGetChildDirectories
                    route "/create-student-directories" >=> AAD.Auth.requiresTeacher >=> handlePostStudentDirectories
                    route "/directory-info" >=> AAD.Auth.requiresTeacher >=> handleGetDirectoryInfo
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
        .UseAuthentication()
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

    let clientId = Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_CLIENT_ID"
    let authority = Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_AUTHORITY"
    Server.addAADAuth services clientId authority

let configureLogging (ctx: HostBuilderContext) (builder : ILoggingBuilder) =
    builder
        .AddFilter(fun l -> ctx.HostingEnvironment.IsDevelopment() || l.Equals LogLevel.Error)
        .AddConsole()
        .AddDebug()
    |> ignore

[<EntryPoint>]
let main args =
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHostBuilder -> webHostBuilder.Configure configureApp |> ignore)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0