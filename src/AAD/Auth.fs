module AAD.Auth

open AAD.Core
open AAD.Domain
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Graph
open Microsoft.Graph.Auth
open Microsoft.Identity.Client

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
        fn authToken userId
    | None -> failwith "Auth token not found."

let withAuthTokenFromHttpContext (ctx: HttpContext) fn =
    withAuthenticationFromHttpContext ctx (fun authToken _ -> fn authToken)

type Role = Admin | Teacher

let getUserRoles authToken userId = async {
    let! groups = getUserGroups authToken userId
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
                let! userRoles = getUserRoles authToken (UserId (ctx.User.ToGraphUserAccount().ObjectId))
                if List.contains role userRoles
                then return! next ctx
                else return! RequestErrors.forbidden HttpHandler.nil next ctx
            with e ->
                return! ServerErrors.internalError (text (sprintf "Error while getting user roles: %O" e)) next ctx
        | None -> return! RequestErrors.unauthorized "Bearer" "Access to HTL utils" HttpHandler.nil next ctx
    }

let requiresAdmin : HttpHandler = requiresRole Admin
let requiresTeacher : HttpHandler = requiresRole Teacher
