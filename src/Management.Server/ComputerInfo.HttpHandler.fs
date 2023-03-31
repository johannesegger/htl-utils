module ComputerInfo.HttpHandler

open Giraffe
open ComputerInfo.Mapping

let getComputerInfo (dataStoreApi: DataStore.DataStoreApi) : HttpHandler =
    fun next ctx -> task {
        let computerInfo =
            dataStoreApi.ReadComputerInfo ()
            |> QueryResult.fromDataStoreDto
        return! Successful.OK computerInfo next ctx
    }
