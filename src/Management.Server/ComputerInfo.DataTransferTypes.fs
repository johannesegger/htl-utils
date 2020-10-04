namespace ComputerInfo.DataTransferTypes

open System
#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

module Result =
    let encode encodeOk encodeError = function
        | Ok v -> Encode.object [ "ok", encodeOk v ]
        | Error v -> Encode.object [ "error", encodeError v ]
    let decoder decodeOk decodeError : Decoder<_> =
        Decode.oneOf [
            Decode.field "ok" decodeOk |> Decode.map Ok
            Decode.field "error" decodeError |> Decode.map Error
        ]

type Bytes = Bytes of int64
module Bytes =
    let encode (Bytes v) = Encode.int64 v
    let decoder : Decoder<_> = Decode.int64 |> Decode.map Bytes
    let toHumanReadable (Bytes v) =
        let rec fn value unit units =
            match units with
            | [] -> (value, unit)
            | _ :: _ when value < 1000. -> (value, unit)
            | x :: xs -> fn (value / 1024.) x xs
        let (value, unit) = fn (float v) "B" [ "KB"; "MB"; "GB"; "TB" ]
        sprintf "%g %s" value unit

type PhysicalMemoryType =
    | PhysicalMemoryTypeDDR2
    | PhysicalMemoryTypeDDR3
    | PhysicalMemoryTypeDDR4
    | UnknownPhysicalMemoryType of int
module PhysicalMemoryType =
    let encode = function
        | PhysicalMemoryTypeDDR2 -> Encode.object [ "ddr2", Encode.nil ]
        | PhysicalMemoryTypeDDR3 -> Encode.object [ "ddr3", Encode.nil ]
        | PhysicalMemoryTypeDDR4 -> Encode.object [ "ddr4", Encode.nil ]
        | UnknownPhysicalMemoryType memoryType -> Encode.object [ "other", Encode.int memoryType ]
    let decoder : Decoder<_> =
        Decode.oneOf [
            Decode.field "ddr2" (Decode.nil PhysicalMemoryTypeDDR2)
            Decode.field "ddr3" (Decode.nil PhysicalMemoryTypeDDR3)
            Decode.field "ddr4" (Decode.nil PhysicalMemoryTypeDDR4)
            Decode.field "other" Decode.int |> Decode.map UnknownPhysicalMemoryType
        ]
    let toString = function
        | PhysicalMemoryTypeDDR2 -> "DDR2"
        | PhysicalMemoryTypeDDR3 -> "DDR3"
        | PhysicalMemoryTypeDDR4 -> "DDR4"
        | UnknownPhysicalMemoryType number -> sprintf "Memory type \"0x%02x\"" number

type PhysicalMemory = {
    Capacity: Bytes option
    MemoryType: PhysicalMemoryType option
}
module PhysicalMemory =
    let encode v =
        Encode.object [
            "capacity", Encode.option Bytes.encode v.Capacity
            "memoryType", Encode.option PhysicalMemoryType.encode v.MemoryType
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Capacity = get.Required.Field "capacity" (Decode.option Bytes.decoder)
                MemoryType = get.Required.Field "memoryType" (Decode.option PhysicalMemoryType.decoder)
            }
        )

type Processor = {
    Name: string option
    NumberOfCores: int option
    NumberOfLogicalProcessors: int option
}
module Processor =
    let encode v =
        Encode.object [
            "name", Encode.option Encode.string v.Name
            "numberOfCores", Encode.option Encode.int v.NumberOfCores
            "numberOfLogicalProcessors", Encode.option Encode.int v.NumberOfLogicalProcessors
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Name = get.Required.Field "name" (Decode.option Decode.string)
                NumberOfCores = get.Required.Field "numberOfCores" (Decode.option Decode.int)
                NumberOfLogicalProcessors = get.Required.Field "numberOfLogicalProcessors" (Decode.option Decode.int)
            }
        )

type GraphicsCard = {
    Name: string option
    AdapterRAM: Bytes option
}
module GraphicsCard =
    let encode v =
        Encode.object [
            "name", Encode.option Encode.string v.Name
            "adapterRAM", Encode.option Bytes.encode v.AdapterRAM
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Name = get.Required.Field "name" (Decode.option Decode.string)
                AdapterRAM = get.Required.Field "adapterRAM" (Decode.option Bytes.decoder)
            }
        )

type QueryConnectionError = QueryConnectionError of string
module QueryConnectionError =
    let encode = function
        | QueryConnectionError msg -> Encode.object [ "queryConnectionError", Encode.string msg ]
    let decoder : Decoder<_> =
        Decode.oneOf [
            Decode.field "queryConnectionError" Decode.string |> Decode.map QueryConnectionError
        ]

type QueryError =
    | InformationNotQueried
    | InformationNotPresent
    | SendQueryError of string
module QueryError =
    let encode = function
        | InformationNotQueried -> Encode.object [ "informationNotQueried", Encode.nil ]
        | InformationNotPresent -> Encode.object [ "informationNotPresent", Encode.nil ]
        | SendQueryError v -> Encode.object [ "sendQueryError", Encode.string v ]
    let decoder : Decoder<_> =
        Decode.oneOf [
            Decode.field "informationNotQueried" (Decode.nil InformationNotQueried)
            Decode.field "informationNotPresent" (Decode.nil InformationNotPresent)
            Decode.field "sendQueryError" Decode.string |> Decode.map SendQueryError
        ]

type IPAddressType =
    | IPv4 of string
    | IPv6 of string
module IPAddressType =
    let encode = function
        | IPv4 v -> Encode.object [ "ipv4", Encode.string v ]
        | IPv6 v -> Encode.object [ "ipv6", Encode.string v ]
    let decoder : Decoder<_> =
        Decode.oneOf [
            Decode.field "ipv4" Decode.string |> Decode.map IPv4
            Decode.field "ipv6" Decode.string |> Decode.map IPv6
        ]

type NetworkInformation = {
    MACAddress: string option
    IPAddresses: IPAddressType list
}
module NetworkInformation =
    let encode v =
        Encode.object [
            "macAddress", Encode.option Encode.string v.MACAddress
            "ipAddresses", (List.map IPAddressType.encode >> Encode.list) v.IPAddresses
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                MACAddress = get.Required.Field "macAddress" (Decode.option Decode.string)
                IPAddresses = get.Required.Field "ipAddresses" (Decode.list IPAddressType.decoder)
            }
        )

type ComputerModel = {
    Manufacturer: string option
    Model: string option
}
module ComputerModel =
    let encode v =
        Encode.object [
            "manufacturer", Encode.option Encode.string v.Manufacturer
            "model", Encode.option Encode.string v.Model
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Manufacturer = get.Required.Field "manufacturer" (Decode.option Decode.string)
                Model = get.Required.Field "model" (Decode.option Decode.string)
            }
        )

type AfterPowerLossBehavior = Off | On | PreviousState
module AfterPowerLossBehavior =
    let encode = function
        | Off -> Encode.string "off"
        | On -> Encode.string "on"
        | PreviousState -> Encode.string "previousState"
    let decoder : Decoder<_> =
            Decode.string
            |> Decode.andThen (function
                | "off" -> Decode.succeed Off
                | "on" -> Decode.succeed On
                | "previousState" -> Decode.succeed PreviousState
                | v -> Decode.fail (sprintf "Invalid value for AfterPowerLossBehavior: \"%s\"" v)
            )

type BIOSSettings = {
    BootOrder: string list option
    NetworkServiceBootEnabled: bool option
    VTxEnabled: bool option
    AfterPowerLossBehavior: AfterPowerLossBehavior option
    WakeOnLanEnabled: bool option
}
module BIOSSettings =
    let encode v =
        Encode.object [
            "bootOrder", Encode.option (List.map Encode.string >> Encode.list) v.BootOrder
            "networkServiceBootEnabled", Encode.option Encode.bool v.NetworkServiceBootEnabled
            "vtxEnabled", Encode.option Encode.bool v.VTxEnabled
            "afterPowerLossBehavior", Encode.option AfterPowerLossBehavior.encode v.AfterPowerLossBehavior
            "wakeOnLanEnabled", Encode.option Encode.bool v.WakeOnLanEnabled
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                BootOrder = get.Required.Field "bootOrder" (Decode.option (Decode.list Decode.string))
                NetworkServiceBootEnabled = get.Required.Field "networkServiceBootEnabled" (Decode.option Decode.bool)
                VTxEnabled = get.Required.Field "vtxEnabled" (Decode.option Decode.bool)
                AfterPowerLossBehavior = get.Required.Field "afterPowerLossBehavior" (Decode.option AfterPowerLossBehavior.decoder)
                WakeOnLanEnabled = get.Required.Field "wakeOnLanEnabled" (Decode.option Decode.bool)
            }
        )

type BIOSSettingsQueryError =
    | QueryError of QueryError
    | UnknownManufacturer of string
    | ManufacturerNotFound
module BIOSSettingsQueryError =
    let encode = function
        | QueryError v -> QueryError.encode v
        | UnknownManufacturer v -> Encode.object [ "unknownManufacturer", Encode.string v ]
        | ManufacturerNotFound -> Encode.object [ "manufacturerNotFound", Encode.nil ]
    let decoder : Decoder<_> =
        Decode.oneOf [
            QueryError.decoder |> Decode.map QueryError
            Decode.field "unknownManufacturer" Decode.string |> Decode.map UnknownManufacturer
            Decode.field "manufacturerNotFound" (Decode.nil ManufacturerNotFound)
        ]

type ComputerInfoData = {
    NetworkInformation: Result<NetworkInformation list, QueryError>
    Model: Result<ComputerModel, QueryError>
    PhysicalMemory: Result<PhysicalMemory list, QueryError>
    Processors: Result<Processor list, QueryError>
    GraphicsCards: Result<GraphicsCard list, QueryError>
    BIOSSettings: Result<BIOSSettings, BIOSSettingsQueryError>
}
module ComputerInfoData =
    let encode v =
        Encode.object [
            "networkInformation", Result.encode (List.map NetworkInformation.encode >> Encode.list) QueryError.encode v.NetworkInformation
            "model", Result.encode ComputerModel.encode QueryError.encode v.Model
            "physicalMemory", Result.encode (List.map PhysicalMemory.encode >> Encode.list) QueryError.encode v.PhysicalMemory
            "processors", Result.encode (List.map Processor.encode >> Encode.list) QueryError.encode v.Processors
            "graphicsCards", Result.encode (List.map GraphicsCard.encode >> Encode.list) QueryError.encode v.GraphicsCards
            "biosSettings", Result.encode BIOSSettings.encode BIOSSettingsQueryError.encode v.BIOSSettings
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                NetworkInformation = get.Required.Field "networkInformation" (Result.decoder (Decode.list NetworkInformation.decoder) QueryError.decoder)
                Model = get.Required.Field "model" (Result.decoder ComputerModel.decoder QueryError.decoder)
                PhysicalMemory = get.Required.Field "physicalMemory" (Result.decoder (Decode.list PhysicalMemory.decoder) QueryError.decoder)
                Processors = get.Required.Field "processors" (Result.decoder (Decode.list Processor.decoder) QueryError.decoder)
                GraphicsCards = get.Required.Field "graphicsCards" (Result.decoder (Decode.list GraphicsCard.decoder) QueryError.decoder)
                BIOSSettings = get.Required.Field "biosSettings" (Result.decoder BIOSSettings.decoder BIOSSettingsQueryError.decoder)
            }
        )

type ComputerInfo = {
    ComputerName: string
    Timestamp: DateTimeOffset
    Data: Result<ComputerInfoData, QueryConnectionError>
}

module ComputerInfo =
    let encode v =
        Encode.object [
            "computerName", Encode.string v.ComputerName
            "timestamp", Encode.datetimeOffset v.Timestamp
            "data", Result.encode ComputerInfoData.encode QueryConnectionError.encode v.Data
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                ComputerName = get.Required.Field "computerName" Decode.string
                Timestamp = get.Required.Field "timestamp" Decode.datetimeOffset
                Data = get.Required.Field "data" (Result.decoder ComputerInfoData.decoder QueryConnectionError.decoder)
            }
        )
