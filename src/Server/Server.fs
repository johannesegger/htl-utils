open System.IO

open System.Net.NetworkInformation
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Giraffe.Serialization
open Saturn
open WakeUp

let publicPath = Path.GetFullPath "../Client/public"
let port = 8085us

let webApp = scope {
    get "/api/send-wakeup-command" (fun next ctx ->
        task {
            let result = WakeUp.sendWakeUpCommand "pc-eggj" (PhysicalAddress.Parse "20-CF-30-81-37-03")
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
