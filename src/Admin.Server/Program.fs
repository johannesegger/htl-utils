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
open System.Net.Http
open System.Text
open Thoth.Json.Giraffe
open Thoth.Json.Net

type FetchError =
    | HttpError of url: string * HttpStatusCode * reasonPhrase: string
    | DecodeError of url: string * message: string

let private httpWithHeaders (ctx: HttpContext) (url: string) httpMethod headers body decoder = async {
    let httpClientFactory = ctx.GetService<IHttpClientFactory>()
    use httpClient = httpClientFactory.CreateClient()
    use requestMessage = new HttpRequestMessage(httpMethod, url)

    headers
    |> Seq.iter (fun (key, value: string) -> requestMessage.Headers.Add(key, value))

    match ctx.Request.Headers.TryGetValue("Authorization") with
    | (true, values) -> requestMessage.Headers.Add("Authorization", values)
    | (false, _) -> ()

    body
    |> Option.iter (fun content -> requestMessage.Content <- new StringContent(Encode.toString 0 content, Encoding.UTF8, "application/json"))

    let! response = httpClient.SendAsync(requestMessage) |> Async.AwaitTask
    let! responseContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
    if not response.IsSuccessStatusCode then
        return Error (HttpError (url, response.StatusCode, responseContent))
    else
        return
            Decode.fromString decoder responseContent
            |> Result.mapError (fun message -> DecodeError(url, message))
}

let httpGet (ctx: HttpContext) url decoder =
    httpWithHeaders ctx url HttpMethod.Get [] None decoder

let httpPost (ctx: HttpContext) url body decoder =
    httpWithHeaders ctx url HttpMethod.Post [] (Some body) decoder

// ---------------------------------
// Web app
// ---------------------------------

let handleGetAutoGroups : HttpHandler =
    fun next ctx -> Successful.OK () next ctx

let requiresRole roleName : HttpHandler =
    fun next ctx -> task {
        let! userRoles = httpGet ctx "http://aad/api/signed-in-user/roles" (Decode.list Decode.string)
        match userRoles with
        | Ok userRoles ->
            if List.contains roleName userRoles
            then return! next ctx
            else return! RequestErrors.forbidden (setBody [||]) next ctx
        | Error e ->
            return! ServerErrors.internalError (setBodyFromString (sprintf "%O" e)) next ctx
    }

let requiresAdmin : HttpHandler = requiresRole "admin"

let getAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let! untisTeachingData = httpGet ctx "http://untis/api/teaching-data" (Decode.list Untis.TeacherInClass.decoder) |> Async.StartChild
        let! sokratesTeachers = httpGet ctx "http://sokrates/api/teachers" (Decode.list Sokrates.Teacher.decoder) |> Async.StartChild
        let! finalThesesMentors = httpGet ctx "http://final-theses/api/mentors" (Decode.list FinalTheses.Mentor.decoder) |> Async.StartChild
        let! aadAutoGroups = httpGet ctx "http://aad/api/auto-groups" (Decode.list AAD.Group.decoder) |> Async.StartChild
        let! aadUsers = httpGet ctx "http://aad/api/users" (Decode.list AAD.User.decoder)

        let! untisTeachingData = untisTeachingData |> Async.map (Result.map (List.choose id))
        let! sokratesTeachers = sokratesTeachers |> Async.map (Result.map (List.choose id))
        let! finalThesesMentors = finalThesesMentors
        let! aadAutoGroups = aadAutoGroups

        let getUpdates aadUsers aadAutoGroups sokratesTeachers teachingData finalThesesMentors =
            let aadUserLookupByUserName =
                aadUsers
                |> List.map (fun (user: AAD.User) -> user.UserName, user.Id)
                |> Map.ofList

            let teacherIds =
                sokratesTeachers
                |> List.choose (fun (t: Sokrates.Teacher) -> Map.tryFind t.ShortName aadUserLookupByUserName)

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
                        |> List.choose (snd >> fun (Untis.TeacherShortName v) -> Map.tryFind v aadUserLookupByUserName)
                        |> List.distinct
                    (sprintf "GrpLehrer%s" schoolClass, teacherIds)
                )

            let formTeacherIds =
                teachingData
                |> List.choose (function
                    | Untis.NormalTeacher _ -> None
                    | Untis.FormTeacher (schoolClass, Untis.TeacherShortName teacherShortName) -> Some teacherShortName
                )
                |> List.choose (flip Map.tryFind aadUserLookupByUserName)
                |> List.distinct

            let aadUserLookupByMailAddress =
                aadUsers
                |> List.collect (fun (user: AAD.User) ->
                    user.MailAddresses
                    |> List.map (fun mailAddress -> CIString mailAddress, user.Id)
                )
                |> Map.ofList

            let finalThesesMentorIds =
                finalThesesMentors
                |> List.choose (fun (m: FinalTheses.Mentor) -> Map.tryFind (CIString m.MailAddress) aadUserLookupByMailAddress)

            let desiredGroups = [
                yield ("GrpLehrer", teacherIds)
                yield! classGroupsWithTeacherIds
                yield ("GrpKV", formTeacherIds)
                yield ("GrpDA-Betreuer", finalThesesMentorIds)
            ]

            let aadUserLookupById =
                aadUsers
                |> List.map (fun (user: AAD.User) -> user.Id, AAD.User.toDto user)
                |> Map.ofList

            let aadAutoGroupsLookupById =
                aadAutoGroups
                |> List.map (fun (group: AAD.Group) -> group.Id, AAD.Group.toDto group)
                |> Map.ofList

            AADGroupUpdates.calculateAll aadAutoGroups desiredGroups
            |> List.map (AAD.GroupModification.toDto aadUserLookupById aadAutoGroupsLookupById)

        return!
            Ok getUpdates
            |> Result.apply (Result.mapError List.singleton aadUsers)
            |> Result.apply (Result.mapError List.singleton aadAutoGroups)
            |> Result.apply (Result.mapError List.singleton sokratesTeachers)
            |> Result.apply (Result.mapError List.singleton untisTeachingData)
            |> Result.apply (Result.mapError List.singleton finalThesesMentors)
            |> function
            | Ok v -> Successful.OK v next ctx
            | Error e -> ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
    }

let applyAADGroupUpdates : HttpHandler =
    fun next ctx -> task {
        let! input = ctx.BindJsonAsync<Shared.AADGroupUpdates.GroupUpdate list>()
        let body =
            input
            |> List.map (AAD.GroupModification.fromDto >> AAD.GroupModification.encode)
            |> Encode.list
        let! result = httpPost ctx "http://aad/api/auto-groups/modify" body (Decode.nil ())
        match result with
        | Ok v -> return! Successful.OK () next ctx
        | Error e -> return! ServerErrors.INTERNAL_ERROR (sprintf "%O" e) next ctx
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
        |> Extra.withCustom Shared.AADGroupUpdates.GroupUpdate.encode Shared.AADGroupUpdates.GroupUpdate.decode
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