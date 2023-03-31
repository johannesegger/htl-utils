namespace DataStore

open DataStore.Configuration
open DataStore.Domain
open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

module Json =
    let serializerOptions =
        let options = JsonSerializerOptions()
        options.Converters.Add(JsonFSharpConverter())
        #if DEBUG
        options.WriteIndented <- true
        #endif
        options

    let toBool (v: obj) = (v :?> JsonElement).GetBoolean()
    let toString (v: obj) = (v :?> JsonElement).GetString()
    let toUInt32 (v: obj) = (v :?> JsonElement).GetUInt32()
    let toUInt64 (v: obj) = (v :?> JsonElement).GetUInt64()
    let toList map (v: obj) = (v :?> JsonElement).EnumerateArray() |> Seq.map map |> Seq.toList

type DataStoreApi(config) =
    member _.ReadComputerInfo () =
        try
            JsonSerializer.Deserialize<QueryResult>(File.ReadAllText config.ComputerInfoFilePath, Json.serializerOptions)
        with e ->
            #if DEBUG
            printfn "Can't read computer info: %O" e
            #endif
            { Timestamp = DateTimeOffset.Now; ComputerInfo = [] }

    member this.UpdateComputerInfo (queryResult: QueryResult) =
        let oldData = this.ReadComputerInfo()
        let oldDataMap =
            oldData.ComputerInfo
            |> List.map (fun computerInfo -> computerInfo.ComputerName, computerInfo)
            |> Map.ofList
        let data =
            { queryResult with
                ComputerInfo =
                    queryResult.ComputerInfo
                    |> List.map (fun computerInfo ->
                        let oldComputerInfo = Map.tryFind computerInfo.ComputerName oldDataMap
                        match computerInfo.Properties, oldComputerInfo with
                        | Ok _, _
                        | _, None
                        | Error _, Some { Properties = Error _ } -> computerInfo
                        | Error _, Some ({ Properties = Ok _ } as v) -> v
                    )
            }
        let text = JsonSerializer.Serialize(data, Json.serializerOptions)
        Directory.CreateDirectory(Path.GetDirectoryName(config.ComputerInfoFilePath)) |> ignore
        File.WriteAllText(config.ComputerInfoFilePath, text)

    static member FromEnvironment () =
        DataStoreApi(Config.fromEnvironment ())
