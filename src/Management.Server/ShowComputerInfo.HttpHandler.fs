module ShowComputerInfo.HttpHandler

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open ShowComputerInfo.Mapping

let getComputerInfo : HttpHandler =
    fun next ctx -> task {
        let computerInfo =
            DataStore.Core.readComputerInfo ()
            |> List.map ComputerInfo.fromDataStoreDto
        return! Successful.OK computerInfo next ctx
    }
