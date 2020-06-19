module Auth

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Thoth.Json.Net

let requiresRole roleName : HttpHandler =
    fun next ctx -> task {
        let! userRoles = Http.``get`` ctx (ServiceUrl.aad "signed-in-user/roles") (Decode.list Decode.string)
        match userRoles with
        | Ok userRoles ->
            if List.contains roleName userRoles
            then return! next ctx
            else return! RequestErrors.forbidden (setBody [||]) next ctx
        | Error e ->
            return! ServerErrors.internalError (text (sprintf "%O" e)) next ctx
    }

let requiresAdmin : HttpHandler = requiresRole "admin"
let requiresTeacher : HttpHandler = requiresRole "teacher"
