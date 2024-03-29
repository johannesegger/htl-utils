module ShowComputerInfo

open Fable.Core
open Fable.React
open Fable.React.Props
open FSharp.Control
open Fulma
open ComputerInfo.DataTransferTypes
open Thoth.Fetch
open Thoth.Json

module BootOrderType =
    let toString = function
        | Legacy -> "Legacy"
        | UEFI -> "UEFI"

type LoadableComputerInfo =
    | LoadingComputerInfo
    | LoadedComputerInfo of QueryResult
    | FailedToLoadComputerInfo

type Model = LoadableComputerInfo

type Msg =
    | LoadComputerInfo
    | LoadComputerInfoResponse of Result<QueryResult, exn>

let update msg model =
    match msg with
    | LoadComputerInfo -> LoadingComputerInfo
    | LoadComputerInfoResponse (Ok queryResult) ->
        { queryResult with
            ComputerInfo =
                queryResult.ComputerInfo
                |> List.sortBy (fun computerInfo -> computerInfo.ComputerName)
        }
        |> LoadedComputerInfo
    | LoadComputerInfoResponse (Error e) ->
        FailedToLoadComputerInfo

let init = LoadingComputerInfo

let view model dispatch =
    let coloredText color text =
        span [ Class (Modifier.parseModifiers [ Modifier.TextColor color ] |> String.concat " ") ] [ str text ]
    let errorText = coloredText IsDanger
    let successText = coloredText IsSuccess
    let warningText = coloredText IsWarning

    let queryErrorView = function
        | InformationNotPresent -> errorText "Information not present"
        | InformationNotQueried -> errorText "Information not queried"
        | SendQueryError e -> errorText (sprintf "Error while querying information: %s" e)

    let computerModelView = function
        | Ok computerModel ->
            [
                match computerModel.Manufacturer with
                | Some manufacturer -> str (sprintf "%s" manufacturer)
                | None -> errorText "<Unknown manufacturer>"

                str " - "

                match computerModel.Model with
                | Some model -> str (sprintf "%s" model)
                | None -> errorText "<Unknown model>"
            ]
        | Error e -> [ queryErrorView e ]

    let networkInformationView = function
        | Ok data ->
            data
            |> List.map (fun entry ->
                div [] [
                    match entry.MACAddress with
                    | Some macAddress -> span [] [ str macAddress ]
                    | None -> errorText "<Unknown MAC address>"

                    str " - "

                    match entry.IPAddresses with
                    | [] -> errorText "<No IP addresses>"
                    | ipAddresses ->
                        yield!
                            [
                                for ipAddress in ipAddresses ->
                                    match ipAddress with
                                    | IPv4 address
                                    | IPv6 address -> str address
                            ]
                            |> List.intersperse (str ", ")
                ]
            )
        | Error e -> [ queryErrorView e ]

    let physicalMemoryView = function
        | Ok data ->
            (Map.empty, data)
            ||> List.fold (fun map entry ->
                match Map.tryFind entry map with
                | Some i -> Map.add entry (i + 1) map
                | None -> Map.add entry 1 map
            )
            |> Map.toList
            |> List.sortByDescending snd
            |> List.map (fun (entry, count) ->
                div [] [
                    str (sprintf "%d x " count)

                    match entry.Capacity with
                    | Some capacity -> span [] [ str (Bytes.toHumanReadable capacity) ]
                    | None -> errorText "<Unknown capacity>"

                    str " "

                    match entry.MemoryType with
                    | Some memoryType -> str (PhysicalMemoryType.toString memoryType)
                    | None -> errorText "<Unknown memory type>"
                ]
            )
        | Error e -> [ queryErrorView e ]

    let processorView = function
        | Ok data ->
            data
            |> List.map (fun (processor: Processor) ->
                div [] [
                    match processor.Name with
                    | Some processorName -> str processorName
                    | None -> errorText "<Unknown name>"

                    str " - "

                    match processor.NumberOfCores with
                    | Some numberOfCores -> str (sprintf "%d cores" numberOfCores)
                    | None -> errorText "<Unknown number of cores>"

                    str ", "

                    match processor.NumberOfLogicalProcessors with
                    | Some numberOfLogicalProcessors -> str (sprintf "%d logical processors" numberOfLogicalProcessors)
                    | None -> errorText "<Unknown number of logical processors>"
                ]
            )
        | Error e -> [ queryErrorView e ]

    let graphicsCardsView = function
        | Ok data ->
            data
            |> List.map (fun (graphicsCard: GraphicsCard) ->
                div [] [
                    match graphicsCard.Name with
                    | Some graphicsCardName -> str graphicsCardName
                    | None -> errorText "<Unknown name>"

                    str " - "

                    match graphicsCard.AdapterRAM with
                    | Some adapterRAM -> str (sprintf "%s RAM" (Bytes.toHumanReadable adapterRAM))
                    | None -> errorText "<Unknown RAM size>"
                ]
            )
        | Error e -> [ queryErrorView e ]

    let biosSettingsView = function
        | Ok data ->
            [
                match data.BootOrder with
                | [] ->
                    yield div [] [
                        str "Boot order: "
                        errorText "Not found"
                    ]
                | x ->
                    for bootOrder in x ->
                        div [] [
                            str (sprintf "Boot order (%s): " (BootOrderType.toString bootOrder.Type))
                            bootOrder.Devices |> String.concat ", " |> str
                        ]
                yield div [] [
                    str "Network boot enabled: "
                    data.NetworkServiceBootEnabled |> Option.map (function | true -> successText "✔" | false -> errorText "✗") |> Option.defaultValue (errorText "Not found")
                ]
                yield div [] [
                    str "VTx enabled: "
                    data.VTxEnabled |> Option.map (function | true -> successText "✔" | false -> errorText "✗") |> Option.defaultValue (errorText "Not found")
                ]
                yield div [] [
                    str "After power loss behavior: "
                    data.AfterPowerLossBehavior |> Option.map (function | Off -> errorText "Off" | On -> successText "On" | PreviousState -> warningText "Previous state") |> Option.defaultValue (errorText "Not found")
                ]
                yield div [] [
                    str "Wake-On-LAN enabled: "
                    data.WakeOnLanEnabled |> Option.map (function | true -> successText "✔" | false -> errorText "✗") |> Option.defaultValue (errorText "Not found")
                ]
            ]
        | Error (QueryError e) -> [ queryErrorView e ]
        | Error (UnknownManufacturer v) -> [ errorText (sprintf "Unknown manufacturer: \"%s\"" v) ]
        | Error ManufacturerNotFound -> [ errorText "Manufacturer not found" ]

    match model with
    | LoadingComputerInfo ->
        Section.section [] [
            Progress.progress [ Progress.Color IsDanger ] []
        ]
    | FailedToLoadComputerInfo ->
        Section.section [] [ Views.errorWithRetryButton "Error while loading computer info" (fun () -> dispatch LoadComputerInfo) ]
    | LoadedComputerInfo queryResult ->
        Section.section [] [
            Heading.h5 [] [
                str "Last query time: "
                let diff = System.DateTimeOffset.Now - queryResult.Timestamp
                let color =
                    if diff < System.TimeSpan.FromDays(1.) then IsSuccess
                    elif diff < System.TimeSpan.FromDays(7.) then IsWarning
                    else IsDanger
                coloredText color ((sprintf "%s %s" (queryResult.Timestamp.ToString("D")) (queryResult.Timestamp.ToString("t"))))
            ]
            Table.table [] [
                thead [] [
                    tr [] [
                        th [] [ str "Computer name" ]
                        th [] [ str "Query timestamp" ]
                        th [] [ str "Manufacturer - Model" ]
                        th [] [ str "NetworkInformation" ]
                        th [] [ str "Physical memory" ]
                        th [] [ str "Processors" ]
                        th [] [ str "Graphics cards" ]
                        th [] [ str "BIOS settings" ]
                    ]
                ]
                tbody [] [
                    for entry in queryResult.ComputerInfo ->
                        tr [] [
                            td [] [ str entry.ComputerName ]
                            td [] [ str (sprintf "%s %s" (entry.Timestamp.ToString("D")) (entry.Timestamp.ToString("t"))) ]
                            match entry.Data with
                            | Ok computerInfoData ->
                                td [] (computerModelView computerInfoData.Model)
                                td [] (networkInformationView computerInfoData.NetworkInformation)
                                td [] (physicalMemoryView computerInfoData.PhysicalMemory)
                                td [] (processorView computerInfoData.Processors)
                                td [] (graphicsCardsView computerInfoData.GraphicsCards)
                                td [] (biosSettingsView computerInfoData.BIOSSettings)
                            | Error (QueryConnectionError e) ->
                                td [ ColSpan 6 ] [ errorText e ]
                        ]
                ]
            ]
    ]

let stream (pageActive: IAsyncObservable<bool>) (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
    pageActive
    |> AsyncRx.flatMapLatest (function
        | true ->
            [
                msgs

                let loadComputerInfo =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync (async {
                            let coders = Extra.empty |> Thoth.addCoders
                            let! (queryResult: QueryResult) = Fetch.get("/api/computer-info", extra = coders) |> Async.AwaitPromise
                            return queryResult
                        })
                        |> AsyncRx.map Ok
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )

                states
                |> AsyncRx.choose (fst >> function | Some LoadComputerInfo -> Some loadComputerInfo | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showSimpleErrorToast (fun e -> "Loading computer info failed", e.Message)
                |> AsyncRx.map LoadComputerInfoResponse

                AsyncRx.single LoadComputerInfo
            ]
            |> AsyncRx.mergeSeq
        | false -> AsyncRx.empty ()
    )
