namespace ComputerInfo.Mapping

open ComputerInfo.DataTransferTypes

module ComputerInfo =
    let fromDataStoreDto (computerInfo: DataStore.Domain.ComputerInfo) =
        let lookup propertyName properties mapError fn =
            match Map.tryFind propertyName properties with
            | Some (Ok entries) -> fn entries
            | Some (Error e) -> Error (SendQueryError e |> mapError)
            | None -> Error (mapError InformationNotQueried)

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
                            lookup "NetAdapter" properties id (fun adapterEntries ->
                                lookup "NetworkAdapterConfiguration" properties id (fun adapterConfigEntries ->
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
                            lookup "ComputerSystem" properties id (fun entries ->
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
                            lookup "PhysicalMemory" properties id (fun entries ->
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
                            lookup "Processor" properties id (fun entries ->
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
                            lookup "VideoController" properties id (fun entries ->
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
                        BIOSSettings =
                            lookup "BIOS" properties QueryError (fun entries ->
                                let manufacturer = entries |> List.tryPick (tryFindOptional "Manufacturer" >> Option.map DataStore.Core.toString)
                                match manufacturer with
                                | Some "Hewlett-Packard" ->
                                    lookup "HP_BIOSSetting" properties QueryError (fun entries ->
                                        let tryParseEnableDisable = function
                                            | "Enable" -> Some true
                                            | "Disable" -> Some false
                                            | _ -> None
                                        let tryParseAfterPowerLossBehavior = function
                                            | "Off" -> Some Off
                                            | "On" -> Some On
                                            | "Previous State" -> Some PreviousState
                                            | _ -> None
                                        let tryFindEntry name valueKey tryParse =
                                            entries
                                            |> List.tryFind (fun entry -> tryFindOptional "Name" entry |> Option.map DataStore.Core.toString = Some name)
                                            |> Option.bind (fun entry -> tryFindOptional valueKey entry)
                                            |> Option.bind tryParse
                                        let bootOrder = tryFindEntry "Boot Order" "Elements" (DataStore.Core.toList DataStore.Core.toString >> Some)
                                        let networkServiceBootEnabled = tryFindEntry "Network Service Boot" "CurrentValue" (DataStore.Core.toString >> tryParseEnableDisable)
                                        let vtxEnabled = tryFindEntry "Virtualization Technology (VTx)" "CurrentValue" (DataStore.Core.toString >> tryParseEnableDisable)
                                        let afterPowerLossBehavior = tryFindEntry "After Power Loss" "CurrentValue" (DataStore.Core.toString >> tryParseAfterPowerLossBehavior)
                                        let wakeOnLanEnabled = tryFindEntry "S5 Wake on LAN" "CurrentValue" (DataStore.Core.toString >> tryParseEnableDisable)
                                        Ok {
                                            BootOrder = bootOrder
                                            NetworkServiceBootEnabled = networkServiceBootEnabled
                                            VTxEnabled = vtxEnabled
                                            AfterPowerLossBehavior = afterPowerLossBehavior
                                            WakeOnLanEnabled = wakeOnLanEnabled
                                        }
                                    )
                                | Some v -> Error (UnknownManufacturer v)
                                | None -> Error ManufacturerNotFound
                            )
                    }
                | Error e -> Error (QueryConnectionError e)
        }
