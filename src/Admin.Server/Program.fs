module App

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System
open Thoth.Json.Giraffe
open Thoth.Json.Net
open System.Net
open System.Net.Http

type FetchError =
    | HttpError of url: string * HttpStatusCode * reasonPhrase: string
    | DecodeError of url: string * message: string

let httpGet (httpClientFactory: IHttpClientFactory) (url: string) decoder = async {
    use httpClient = httpClientFactory.CreateClient()
    let! response = httpClient.GetAsync url |> Async.AwaitTask
    if not response.IsSuccessStatusCode then
        return Error (HttpError (url, response.StatusCode, response.ReasonPhrase))
    else
        let! responseContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        return
            Decode.fromString decoder responseContent
            |> Result.mapError (fun message -> DecodeError(url, message))
}

// ---------------------------------
// Web app
// ---------------------------------

let handleGetAutoGroups : HttpHandler =
    fun next ctx -> Successful.OK () next ctx

let requiresUser preferredUsername : HttpHandler =
    authorizeUser
        (fun user -> user.HasClaim("preferred_username", preferredUsername))
        (RequestErrors.forbidden (setBody [||]))

let requiresAdmin : HttpHandler = requiresUser "admin@htlvb.at"

let getAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let httpClientFactory = ctx.GetService<IHttpClientFactory>()
        let! teachingData = httpGet httpClientFactory "http://untis/api/teaching-data" (Decode.list Untis.TeacherInClass.decoder) |> Async.StartChild
        let! sokratesTeachers = httpGet httpClientFactory "http://sokrates/api/teachers" (Decode.list Sokrates.Teacher.decoder) |> Async.StartChild
        // let! finalThesesMentors = httpGet httpClientFactory "http://final-theses/api/mentors" |> Async.StartChild
        let! aadAutoGroups = httpGet httpClientFactory "http://aad/api/auto-groups" (Decode.list AAD.Group.decoder) |> Async.StartChild
        let! aadTeachers = httpGet httpClientFactory "http://aad/api/teachers" (Decode.list AAD.User.decoder)

        let! untisTeachingData = teachingData |> Async.map (Result.map (List.choose id))
        let! sokratesTeachers = sokratesTeachers |> Async.map (Result.map (List.choose id))
        let! aadAutoGroups = aadAutoGroups

        let getUpdates aadTeachers aadAutoGroups sokratesTeachers teachingData =
            let aadUserMap =
                aadTeachers
                |> List.map (fun (user: AAD.User) -> user.UserName, user.Id)
                |> Map.ofList

            let teacherIds =
                sokratesTeachers
                |> List.choose (fun (t: Sokrates.Teacher) -> Map.tryFind t.ShortName aadUserMap)

            let classGroupsWithTeacherIds =
                teachingData
                |> List.map (function
                    | Untis.NormalTeacher (schoolClass, teacherShortName)
                    | Untis.FormTeacher (schoolClass, teacherShortName) -> (schoolClass, teacherShortName)
                )
                |> List.groupBy fst
                |> List.map (fun (Untis.SchoolClass schoolClass, teachers) ->
                    let teacherIds =
                        teachers
                        |> List.choose (snd >> fun (Untis.TeacherShortName v) -> Map.tryFind v aadUserMap)
                    (sprintf "GrpLehrer%s" schoolClass, teacherIds)
                )

            let formTeacherIds =
                teachingData
                |> List.choose (function
                    | Untis.NormalTeacher _ -> None
                    | Untis.FormTeacher (schoolClass, Untis.TeacherShortName teacherShortName) -> Some teacherShortName
                )
                |> List.choose (flip Map.tryFind aadUserMap)

            let desiredGroups = [
                yield ("GrpLehrer", teacherIds)
                yield! classGroupsWithTeacherIds
                yield ("GrpKV", formTeacherIds)
                yield ("GrpDA-Betreuer", []) // TODO
            ]

            AADGroupUpdates.calculateAll aadAutoGroups desiredGroups

        return!
            Ok getUpdates
            |> Result.apply (Result.mapError List.singleton aadTeachers)
            |> Result.apply (Result.mapError List.singleton aadAutoGroups)
            |> Result.apply (Result.mapError List.singleton sokratesTeachers)
            |> Result.apply (Result.mapError List.singleton untisTeachingData)
            |> function
            | Ok v -> Successful.OK v next ctx
            | Error e -> ServerErrors.INTERNAL_ERROR e next ctx
    }

let applyAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        return! Successful.OK () next ctx
    }

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/aad/group-updates" >=> requiresAdmin >=> getAADGroupUpdates
                ]
                POST >=> choose [
                    route "/aad/group-updates/apply" >=> requiresAdmin >=> applyAADGroupUpdates
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
        // |> Extra.withCustom Group.encode Group.decoder
        // |> Extra.withCustom User.encode User.decoder
    services.AddSingleton<IJsonSerializer>(ThothSerializer(isCamelCase = true, extra = coders)) |> ignore
    services
        .AddAuthentication(fun config ->
            config.DefaultScheme <- JwtBearerDefaults.AuthenticationScheme
            config.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(fun config ->
            config.Audience <- Environment.getEnvVarOrFail "MICROSOFT_GRAPH_CLIENT_ID"
            config.Authority <- Environment.getEnvVarOrFail "MICROSOFT_GRAPH_AUTHORITY"
            config.TokenValidationParameters.ValidateIssuer <- false
            config.TokenValidationParameters.SaveSigninToken <- true
        ) |> ignore

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