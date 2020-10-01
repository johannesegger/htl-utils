module ComputerInfo.HttpHandler

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open ComputerInfo.Mapping

let getComputerInfo dataStoreConfig : HttpHandler =
    fun next ctx -> task {
        let computerInfo =
            DataStore.Core.readComputerInfo
            |> Reader.run dataStoreConfig
            |> List.map ComputerInfo.fromDataStoreDto
        return! Successful.OK computerInfo next ctx
    }
