open System.IO
open System.Threading.Tasks

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Saturn
open Shared

open Giraffe.Serialization
open System.Diagnostics

let publicPath = Path.GetFullPath "../Client/public"
let port = 8085us

let sendWakeUpCommand() = task {
    let p = Process.Start("wakeonlan", "20:CF:30:81:37:03")
    p.WaitForExit()
    if p.ExitCode <> 0 then ()
    else failwith "Error while running `wakeonlan`"
}

let webApp = scope {
    get "/api/send-wakeup-command" (fun next ctx ->
        task {
            do! sendWakeUpCommand()
            return! Successful.OK () next ctx
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
