module AAD.Auth

open AAD.Configuration
open AAD.Core
open AAD.Domain
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Graph
open Microsoft.Graph.Auth
open Microsoft.Identity.Client

let private clientApp = reader {
    let! config = Reader.environment
    return ConfidentialClientApplicationBuilder
        .Create(config.GraphClientId)
        .WithClientSecret(config.GraphClientSecret)
        .WithRedirectUri("https://localhost:8080")
        .Build()
}

let private authProvider = clientApp |> Reader.map OnBehalfOfProvider

let private graphServiceClient = authProvider |> Reader.map GraphServiceClient

let private graphClient authToken = reader {
    let! graphServiceClient = graphServiceClient
    return { Client = graphServiceClient; Authentication = OnBehalfOf authToken }
}

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

let withAuthenticationFromHttpContext (ctx: HttpContext) fn = reader {
    match tryGetBearerTokenFromHttpRequest ctx.Request with
    | Some authToken ->
        let userId = UserId (ctx.User.ToGraphUserAccount().ObjectId)
        let! graphClient = graphClient authToken
        return! fn graphClient userId
    | None -> return failwith "Auth token not found."
}

let withAuthTokenFromHttpContext (ctx: HttpContext) fn =
    withAuthenticationFromHttpContext ctx (fun graphClient _ -> fn graphClient)

type Role = Admin | Teacher

let getUserRoles graphClient userId = asyncReader {
    let! config = Reader.environment |> AsyncReader.liftReader
    let! groups = getUserGroups graphClient userId |> AsyncReader.liftAsync
    return
        groups
        |> List.choose (function
            | :? DirectoryRole as role when CIString role.Id = CIString config.GlobalAdminRoleId -> Some Admin
            | :? Group as group when CIString group.Id = CIString config.TeacherGroupId -> Some Teacher
            | _ -> None
        )
        |> List.distinct
}

let requiresRole config role : HttpHandler =
    fun next ctx -> task {
        match tryGetBearerTokenFromHttpRequest ctx.Request with
        | Some authToken ->
            let! result = async {
                try
                    let graphClient = Reader.run config (graphClient authToken)
                    let! userRoles = Reader.run config (getUserRoles graphClient (UserId (ctx.User.ToGraphUserAccount().ObjectId)))
                    if List.contains role userRoles
                    then return next ctx
                    else return RequestErrors.forbidden HttpHandler.nil next ctx
                with e ->
                    return ServerErrors.internalError (text (sprintf "Error while getting user roles: %O" e)) next ctx
            }
            return! result
        | None -> return! RequestErrors.unauthorized "Bearer" "Access to HTL utils" HttpHandler.nil next ctx
    }

let requiresAdmin config : HttpHandler = requiresRole config Admin
let requiresTeacher config : HttpHandler = requiresRole config Teacher
