module App

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System
open System.Threading
open System.Threading.Tasks

let queryComputerInfo =
    AD.Core.getComputers ()
    |> List.map CIM.Core.getComputerInfo
    |> Async.Parallel
    |> Async.map List.ofArray

let computerInfoToDbDto (computerInfo: CIM.Domain.ComputerInfo) =
    {
        DataStore.Domain.ComputerName = computerInfo.ComputerName
        DataStore.Domain.Timestamp = computerInfo.Timestamp
        DataStore.Domain.Properties =
            match computerInfo.Properties with
            | Ok properties ->
                properties
                |> Map.map (fun _ value ->
                    match value with
                    | Ok value -> Ok value
                    | Error (CIM.Domain.SendQueryError e) -> Error e.Message
                )
                |> Ok
            | Error (CIM.Domain.ConnectionError e) -> Error e.Message
    }

type Worker(logger: ILogger<Worker>) =
    inherit BackgroundService()

    override _.ExecuteAsync(stop: CancellationToken) =
        async {
            while true do
                try
                    logger.LogInformation("{time}: Querying computer info.", DateTimeOffset.Now)
                    let! computerInfo = queryComputerInfo
                    logger.LogInformation("{time}: Storing computer info.", DateTimeOffset.Now)
                    computerInfo |> List.map computerInfoToDbDto |> DataStore.Core.updateComputerInfo
                    logger.LogInformation("{time}: Done.", DateTimeOffset.Now)
                with e ->
                    logger.LogError("{time}: Unhandled exception: {exception}", DateTimeOffset.Now, e)
                do! Async.Sleep(TimeSpan.FromMinutes(30.).TotalMilliseconds |> int)
        }
        |> fun wf -> Async.StartAsTask(wf, cancellationToken = stop) :> Task

let configureLogging (ctx: HostBuilderContext) (builder : ILoggingBuilder) =
    builder
        .AddFilter(fun l -> ctx.HostingEnvironment.IsDevelopment() || l >= LogLevel.Error)
        .AddConsole()
        .AddDebug()
    |> ignore

[<EntryPoint>]
let main args =
    Host.CreateDefaultBuilder(args)
        .UseWindowsService()
        .ConfigureHostConfiguration(fun configHost ->
            configHost.AddEnvironmentVariables(prefix = "ASPNETCORE_") |> ignore
        )
        .ConfigureServices(fun ctx services ->
            services.AddHostedService<Worker>() |> ignore
        )
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
