module DataStore.Core

open DataStore.Configuration
open DataStore.Domain
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
        return
            JsonSerializer.Deserialize<ComputerInfo array>(File.ReadAllText config.ComputerInfoFilePath, serializerOptions)
            |> Array.toList
    with e ->
        #if DEBUG
        printfn "Can't read computer info: %O" e
        #endif
        return []
}

let updateComputerInfo (computerInfo: ComputerInfo list) = reader {
    let! oldData = readComputerInfo
    let oldDataMap =
        oldData
        |> List.map (fun computerInfo -> computerInfo.ComputerName, computerInfo)
        |> Map.ofList
    let data =
        computerInfo
        |> List.map (fun computerInfo ->
            match computerInfo.Properties with
            | Ok _ -> computerInfo
            | Error _ ->
                Map.tryFind computerInfo.ComputerName oldDataMap
                |> Option.defaultValue computerInfo
        )
    let text = JsonSerializer.Serialize(data, serializerOptions)
    let! config = Reader.environment
    Directory.CreateDirectory(Path.GetDirectoryName(config.ComputerInfoFilePath)) |> ignore
    File.WriteAllText(config.ComputerInfoFilePath, text)
}
