module ComputerInfo.HttpHandler

open Giraffe
open ComputerInfo.Mapping

let getComputerInfo dataStoreConfig : HttpHandler =
    fun next ctx -> task {
        let computerInfo =
            DataStore.Core.readComputerInfo
            |> Reader.run dataStoreConfig
            |> QueryResult.fromDataStoreDto
        return! Successful.OK computerInfo next ctx
    }
