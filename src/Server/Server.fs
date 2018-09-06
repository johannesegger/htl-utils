open System.IO

open Microsoft.Extensions.DependencyInjection
open Giraffe
open Saturn

open Giraffe.Serialization
open System.Net
open System.Net.Sockets
open System.Net.NetworkInformation

let publicPath = Path.GetFullPath "../Client/public"
let port = 8085us

type WakeUpError =
    | HostResolutionError of string
    | GetIpAddressFromHostNameError of string * IPAddress list
    | WakeOnLanError of IPAddress * PhysicalAddress

let sendWakeUpCommand (hostName: string) (macAddress: PhysicalAddress) = Result.result {
    let! hostEntry =
        try
            Dns.GetHostEntry hostName
            |> Ok
        with e ->
            HostResolutionError hostName
            |> Error

    let! ipAddress =
        hostEntry.AddressList
        |> Seq.tryFind (fun p -> p.AddressFamily = AddressFamily.InterNetwork)
        |> Option.orElse (Seq.tryHead hostEntry.AddressList)
        |> Result.ofOption (GetIpAddressFromHostNameError (hostName, List.ofArray hostEntry.AddressList))

    return!
        try
            ipAddress.SendWol macAddress
            Ok ()
        with e ->
            WakeOnLanError (ipAddress, macAddress)
            |> Error
}

let webApp = scope {
    get "/api/send-wakeup-command" (fun next ctx ->
        task {
            let result = sendWakeUpCommand "pc-eggj" (PhysicalAddress([| 0x20uy; 0xCFuy; 0x30uy; 0x81uy; 0x37uy; 0x03uy |]))
            return!
                match result with
                | Ok () -> Successful.OK () next ctx
                | Error (HostResolutionError hostName) ->
                    ServerErrors.internalError (setBodyFromString (sprintf "Error while resolving host name \"%s\"" hostName)) next ctx
                | Error (GetIpAddressFromHostNameError (hostName, addresses)) ->
                    ServerErrors.internalError (setBodyFromString (sprintf "Error while getting IP address from host name \"%s\". Address candidates: %A" hostName addresses)) next ctx
                | Error (WakeOnLanError (ipAddress, physicalAddress)) ->
                    ServerErrors.internalError (setBodyFromString (sprintf "Error while sending WoL magic packet to %O (MAC address %O)" ipAddress physicalAddress)) next ctx
        })
}

let configureSerialization (services:IServiceCollection) =
    let fableJsonSettings = Newtonsoft.Json.JsonSerializerSettings()
    fableJsonSettings.Converters.Add(Fable.JsonConverter())
    services.AddSingleton<IJsonSerializer>(NewtonsoftJsonSerializer fableJsonSettings)

let app = application {
    url ("http://0.0.0.0:" + port.ToString() + "/")
    router webApp
    memory_cache
    use_static publicPath
    service_config configureSerialization
    use_gzip
}

run app