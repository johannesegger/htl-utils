module HttpHandler

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe.Core

let nil : HttpHandler = fun next ctx -> task { return Some ctx }
