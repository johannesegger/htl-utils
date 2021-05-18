module AAD.Auth

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe

type Role = Admin | Teacher
module Role =
    let toString = function
        | Admin -> "admin"
        | Teacher -> "teacher"

let requiresRole role : HttpHandler =
    fun next ctx -> task {
        if ctx.User.IsInRole(Role.toString role) then return! next ctx
        else return! RequestErrors.unauthorized "Bearer" "Access to HTL utils" HttpHandler.nil next ctx
    }

let requiresAdmin : HttpHandler = requiresRole Admin
let requiresTeacher : HttpHandler = requiresRole Teacher
