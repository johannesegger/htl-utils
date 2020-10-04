module CIM.Core

open CIM.Configuration
open CIM.Domain
open Microsoft.Management.Infrastructure
open Microsoft.Management.Infrastructure.Options
open System
open System.Net

let private awaitObservableList (obs: IObservable<_>) =
    Async.FromContinuations (fun (cont, econt, ccont) ->
        let mutable result = Collections.Generic.List<_>()
        let obv =
            { new IObserver<_> with
                member _.OnNext(v) =
                    result.Add(v)
                member _.OnCompleted() =
                    cont (Seq.toList result)
                member _.OnError(v) = econt v
            }
        obs.Subscribe(obv) |> ignore
    )

let private awaitObservable (obs: IObservable<_>) = async {
    let! list = awaitObservableList obs
    return List.exactlyOne list
}

let private runInSession computerName fn = asyncReader {
    let! config = Reader.environment |> AsyncReader.liftReader
    let credentials = NetworkCredential(config.UserName, config.Password, config.Domain)
    try
        let credentials = CimCredential(PasswordAuthenticationMechanism.Default, credentials.Domain, credentials.UserName, credentials.SecurePassword)
        use sessionOptions = new WSManSessionOptions()
        sessionOptions.AddDestinationCredentials(credentials)

        use! cimSession = CimSession.CreateAsync(computerName, sessionOptions) |> awaitObservable |> AsyncReader.liftAsync
        do! cimSession.TestConnectionAsync () |> awaitObservableList |> Async.Ignore |> AsyncReader.liftAsync

        let! ct = Async.CancellationToken |> AsyncReader.liftAsync
        use queryOptions = new CimOperationOptions(Timeout = TimeSpan.FromMinutes(1.), CancellationToken = Nullable ct)
        let query namespaceName queryString = async {
            try
                let! queryInstances = cimSession.QueryInstancesAsync(namespaceName, "WQL", queryString, queryOptions) |> awaitObservableList
                return
                    queryInstances
                    |> List.map (fun instance ->
                        instance.CimInstanceProperties
                        |> Seq.map (fun property -> property.Name, property.Value)
                        |> Map.ofSeq
                    )
                    |> Ok
            with e -> return Error (SendQueryError e)
        }
        let! result = fn query |> AsyncReader.liftAsync
        return Ok result
    with e -> return Error (ConnectionError e)
}

let getComputerInfo computerName = asyncReader {
    let timestamp = DateTimeOffset.Now
    let! properties = runInSession computerName (fun query -> async {
        let! bios = query @"root\cimv2" "SELECT * FROM Win32_BIOS"
        let! hpBiosSetting = query @"root\HP\InstrumentedBIOS" "SELECT * FROM HP_BIOSSetting"
        let! computerSystem = query @"root\cimv2" "SELECT * FROM CIM_ComputerSystem"
        let! diskDrive = query @"root\cimv2" "SELECT * FROM CIM_DiskDrive"
        let! logicalLocalDisk = query @"root\cimv2" "SELECT * FROM Win32_LogicalDisk WHERE DriveType = 3"
        let! netAdapter = query @"root\StandardCimv2" "SELECT * FROM MSFT_NetAdapter"
        let! networkAdapterConfiguration = query @"root\cimv2" "SELECT * FROM Win32_NetworkAdapterConfiguration"
        let! operatingSystem = query @"root\cimv2" "SELECT * FROM CIM_OperatingSystem"
        let! physicalMemory = query @"root\cimv2" "SELECT * FROM CIM_PhysicalMemory"
        let! processor = query @"root\cimv2" "SELECT * FROM CIM_Processor"
        let! videoController = query @"root\cimv2" "SELECT * FROM CIM_VideoController"
        return
            [
                "BIOS", bios
                "HP_BIOSSetting", hpBiosSetting
                "ComputerSystem", computerSystem
                "DiskDrive", diskDrive
                "LogicalLocalDisk", logicalLocalDisk
                "NetAdapter", netAdapter
                "NetworkAdapterConfiguration", networkAdapterConfiguration
                "OperatingSystem", operatingSystem
                "PhysicalMemory", physicalMemory
                "Processor", processor
                "VideoController", videoController
            ]
            |> Map.ofList
    })
    return {
        ComputerName = computerName
        Timestamp = timestamp
        Properties = properties
    }
}
