namespace ShowComputerInfo.Mapping

open ShowComputerInfo.DataTransferTypes

module ComputerInfo =
    let fromDataStoreDto (computerInfo: DataStore.Domain.ComputerInfo) =
        let lookup propertyName properties fn =
            match Map.tryFind propertyName properties with
            | Some (Ok entries) -> fn entries
            | Some (Error e) -> Error (SendQueryError e)
            | None -> Error InformationNotQueried

        let tryFindOptional key = Map.tryFind key >> Option.bind Option.ofObj

        let tryParseIPAddress (text: string) =
            match text |> tryDo System.Net.IPAddress.TryParse with
            | Some v when v.AddressFamily = System.Net.Sockets.AddressFamily.InterNetwork -> Some (IPv4 text)
            | Some v when v.AddressFamily = System.Net.Sockets.AddressFamily.InterNetworkV6 -> Some (IPv6 text)
            | _ -> None

        {
            ComputerName = computerInfo.ComputerName
            Timestamp = computerInfo.Timestamp
            Data =
                match computerInfo.Properties with
                | Ok properties ->
                    Ok {
                        NetworkInformation =
                            lookup "NetAdapter" properties (fun adapterEntries ->
                                lookup "NetworkAdapterConfiguration" properties (fun adapterConfigEntries ->
                                    adapterEntries
                                    |> List.filter (fun entry -> tryFindOptional "ConnectorPresent" entry |> Option.map DataStore.Core.toBool |> Option.defaultValue false)
                                    |> List.choose (fun entry -> tryFindOptional "InterfaceIndex" entry |> Option.map (DataStore.Core.toUInt32 >> int))
                                    |> List.choose (fun interfaceIndex ->
                                        adapterConfigEntries
                                        |> List.tryFind (fun entry -> tryFindOptional "InterfaceIndex" entry |> Option.map (DataStore.Core.toUInt32 >> int) = Some interfaceIndex)
                                    )
                                    |> List.map (fun entry ->
                                        {
                                            MACAddress = tryFindOptional "MACAddress" entry |> Option.map DataStore.Core.toString
                                            IPAddresses = tryFindOptional "IPAddress" entry |> Option.map (DataStore.Core.toList DataStore.Core.toString >> List.choose tryParseIPAddress) |> Option.defaultValue []
                                        }
                                    )
                                    |> Ok
                                )
                            )
                        Model =
                            lookup "ComputerSystem" properties (fun entries ->
                                let list =
                                    entries
                                    |> List.map (fun entry ->
                                        let manufacturer = tryFindOptional "Manufacturer" entry |> Option.map DataStore.Core.toString
                                        let model = tryFindOptional "Model" entry |> Option.map DataStore.Core.toString
                                        (manufacturer, model)
                                    )
                                Ok {
                                    Manufacturer = List.tryPick fst list
                                    Model = List.tryPick snd list
                                }
                            )
                        PhysicalMemory =
                            lookup "PhysicalMemory" properties (fun entries ->
                                entries
                                |> List.map (fun entry ->
                                    // see https://docs.microsoft.com/en-us/windows/win32/cimwin32prov/win32-physicalmemory
                                    let capacity = tryFindOptional "Capacity" entry |> Option.map (DataStore.Core.toUInt64 >> int64 >> Bytes)
                                    let memoryType =
                                        tryFindOptional "SMBIOSMemoryType" entry
                                        |> Option.map (fun v ->
                                            match DataStore.Core.toUInt32 v |> int with
                                            | 0x13 -> PhysicalMemoryTypeDDR2
                                            | 0x18 -> PhysicalMemoryTypeDDR3
                                            | 0x1A -> PhysicalMemoryTypeDDR4
                                            | memoryType -> UnknownPhysicalMemoryType memoryType
                                        )
                                    {
                                        Capacity = capacity
                                        MemoryType = memoryType
                                    }
                                )
                                |> Ok
                            )
                        Processors =
                            lookup "Processor" properties (fun entries ->
                                entries
                                |> List.map (fun entry ->
                                    let name = tryFindOptional "Name" entry |> Option.map DataStore.Core.toString
                                    let numberOfCores = tryFindOptional "NumberOfCores" entry |> Option.map (DataStore.Core.toUInt32 >> int)
                                    let numberOfLogicalProcessors = tryFindOptional "NumberOfLogicalProcessors" entry |> Option.map (DataStore.Core.toUInt32 >> int)
                                    {
                                        Name = name
                                        NumberOfCores = numberOfCores
                                        NumberOfLogicalProcessors = numberOfLogicalProcessors
                                    }
                                )
                                |> Ok
                            )
                        GraphicsCards =
                            lookup "VideoController" properties (fun entries ->
                                entries
                                |> List.map (fun entry ->
                                    let name = tryFindOptional "Name" entry |> Option.map DataStore.Core.toString
                                    let adapterRam = tryFindOptional "AdapterRAM" entry |> Option.map (DataStore.Core.toUInt32 >> int64 >> Bytes)
                                    {
                                        Name = name
                                        AdapterRAM = adapterRam
                                    }
                                )
                                |> Ok
                            )
                    }
                | Error e -> Error (QueryConnectionError e)
        }
