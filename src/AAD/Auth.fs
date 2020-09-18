module AAD.Auth

open AAD.Core
open AAD.Domain
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Graph
open Microsoft.Graph.Auth
open Microsoft.Identity.Client

let private clientApp =
    ConfidentialClientApplicationBuilder
        .Create(Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_CLIENT_ID")
        .WithClientSecret(Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_APP_KEY")
        .WithRedirectUri("https://localhost:8080")
        .Build()

let private authProvider = OnBehalfOfProvider(clientApp)

let private graphServiceClient = GraphServiceClient(authProvider)

let tryGetBearerTokenFromHttpRequest (request: HttpRequest) =
    match request.Headers.TryGetValue "Authorization" with
    | (true, values) ->
        values
        |> Seq.tryPick (fun v ->
            if v |> String.startsWithCaseInsensitive "Bearer "
            then Some (v.Substring ("Bearer ".Length) |> UserAssertion)
            else None
        )
    | _ -> None

let withAuthenticationFromHttpContext (ctx: HttpContext) fn =
    match tryGetBearerTokenFromHttpRequest ctx.Request with
    | Some authToken ->
        let userId = UserId (ctx.User.ToGraphUserAccount().ObjectId)
        fn { Client = graphServiceClient; Authentication = OnBehalfOf authToken } userId
    | None -> failwith "Auth token not found."

let withAuthTokenFromHttpContext (ctx: HttpContext) fn =
    withAuthenticationFromHttpContext ctx (fun graphClient _ -> fn graphClient)

type Role = Admin | Teacher

let getUserRoles graphClient userId = async {
    let! groups = getUserGroups graphClient userId
    return
        groups
        |> List.choose (function
            | :? DirectoryRole as role when CIString role.Id = CIString (Environment.getEnvVarOrFail "AAD_GLOBAL_ADMIN_ROLE_ID") -> Some Admin
            | :? Group as group when CIString group.Id = CIString (Environment.getEnvVarOrFail "AAD_TEACHER_GROUP_ID") -> Some Teacher
            | _ -> None
        )
        |> List.distinct
}

let requiresRole role : HttpHandler =
    fun next ctx -> task {
        match tryGetBearerTokenFromHttpRequest ctx.Request with
        | Some authToken ->
            try
                let! userRoles = getUserRoles { Client = graphServiceClient; Authentication = OnBehalfOf authToken } (UserId (ctx.User.ToGraphUserAccount().ObjectId))
                if List.contains role userRoles
                then return! next ctx
                else return! RequestErrors.forbidden HttpHandler.nil next ctx
            with e ->
                return! ServerErrors.internalError (text (sprintf "Error while getting user roles: %O" e)) next ctx
        | None -> return! RequestErrors.unauthorized "Bearer" "Access to HTL utils" HttpHandler.nil next ctx
    }

let requiresAdmin : HttpHandler = requiresRole Admin
let requiresTeacher : HttpHandler = requiresRole Teacher
