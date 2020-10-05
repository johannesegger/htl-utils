module DataStore.Core

open DataStore.Configuration
open DataStore.Domain
open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

let private serializerOptions =
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

let readComputerInfo = reader {
    let! config = Reader.environment
    try
        return JsonSerializer.Deserialize<QueryResult>(File.ReadAllText config.ComputerInfoFilePath, serializerOptions)
    with e ->
        #if DEBUG
        printfn "Can't read computer info: %O" e
        #endif
        return { Timestamp = DateTimeOffset.Now; ComputerInfo = [] }
}

let updateComputerInfo (queryResult: QueryResult) = reader {
    let! oldData = readComputerInfo
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
    let text = JsonSerializer.Serialize(data, serializerOptions)
    let! config = Reader.environment
    Directory.CreateDirectory(Path.GetDirectoryName(config.ComputerInfoFilePath)) |> ignore
    File.WriteAllText(config.ComputerInfoFilePath, text)
}
