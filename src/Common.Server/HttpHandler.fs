module HttpHandler

open Giraffe.Core

let nil : HttpHandler = fun next ctx -> next ctx
