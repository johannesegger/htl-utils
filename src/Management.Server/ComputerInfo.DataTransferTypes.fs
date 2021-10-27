namespace ComputerInfo.DataTransferTypes

open System
#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

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
    let toString = function
        | PhysicalMemoryTypeDDR2 -> "DDR2"
        | PhysicalMemoryTypeDDR3 -> "DDR3"
        | PhysicalMemoryTypeDDR4 -> "DDR4"
        | UnknownPhysicalMemoryType number -> sprintf "Memory type \"0x%02x\"" number

type PhysicalMemory = {
    Capacity: Bytes option
    MemoryType: PhysicalMemoryType option
}

type Processor = {
    Name: string option
    NumberOfCores: int option
    NumberOfLogicalProcessors: int option
}

type GraphicsCard = {
    Name: string option
    AdapterRAM: Bytes option
}

type QueryConnectionError = QueryConnectionError of string

type QueryError =
    | InformationNotQueried
    | InformationNotPresent
    | SendQueryError of string

type IPAddressType =
    | IPv4 of string
    | IPv6 of string

type NetworkInformation = {
    MACAddress: string option
    IPAddresses: IPAddressType list
}

type ComputerModel = {
    Manufacturer: string option
    Model: string option
}

type AfterPowerLossBehavior = Off | On | PreviousState

type BootOrderType =
    | Legacy
    | UEFI

type BootOrder = {
    Type: BootOrderType
    Devices: string list
}

type BIOSSettings = {
    BootOrder: BootOrder list
    NetworkServiceBootEnabled: bool option
    VTxEnabled: bool option
    AfterPowerLossBehavior: AfterPowerLossBehavior option
    WakeOnLanEnabled: bool option
}

type BIOSSettingsQueryError =
    | QueryError of QueryError
    | UnknownManufacturer of string
    | ManufacturerNotFound

type ComputerInfoData = {
    NetworkInformation: Result<NetworkInformation list, QueryError>
    Model: Result<ComputerModel, QueryError>
    PhysicalMemory: Result<PhysicalMemory list, QueryError>
    Processors: Result<Processor list, QueryError>
    GraphicsCards: Result<GraphicsCard list, QueryError>
    BIOSSettings: Result<BIOSSettings, BIOSSettingsQueryError>
}

type ComputerInfo = {
    ComputerName: string
    Timestamp: DateTimeOffset
    Data: Result<ComputerInfoData, QueryConnectionError>
}

type QueryResult = {
    Timestamp: DateTimeOffset
    ComputerInfo: ComputerInfo list
}

module Thoth =
    let addCoders =
        Extra.withCustom Bytes.encode Bytes.decoder
