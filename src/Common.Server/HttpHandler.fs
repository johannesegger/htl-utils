module HttpHandler

open Giraffe.Core

let nil : HttpHandler = fun _ ctx -> task { return Some ctx }
