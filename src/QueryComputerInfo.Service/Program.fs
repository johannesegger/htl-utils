module App

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System
open System.Threading.Tasks
open Quartz

let private adConfig = AD.Configuration.Config.fromEnvironment ()
let private cimConfig = CIM.Configuration.Config.fromEnvironment ()
let private dataStoreConfig = DataStore.Configuration.Config.fromEnvironment ()

type QueryResult = {
    Timestamp: DateTimeOffset
    ComputerInfo: CIM.Domain.ComputerInfo list
}

let queryComputerInfo = async {
    let timestamp = DateTimeOffset.Now
    let! computerInfo =
        // Reader.run adConfig AD.Core.getComputers
        [ "HannesPC" ]
        |> List.map (CIM.Core.getComputerInfo >> Reader.run cimConfig)
        |> Async.Parallel
    return {
        Timestamp = timestamp
        ComputerInfo = List.ofArray computerInfo
    }
}

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

type QueryComputerInfoJob(logger: ILogger<QueryComputerInfoJob>) =
    interface IJob with
        member _.Execute(ctx: IJobExecutionContext) =
            async {
                logger.LogInformation("{time}: Querying computer info.", DateTimeOffset.Now)
                let! queryResult = queryComputerInfo
                logger.LogInformation("{time}: Storing computer info.", DateTimeOffset.Now)
                DataStore.Core.updateComputerInfo {
                    DataStore.Domain.QueryResult.Timestamp = queryResult.Timestamp
                    DataStore.Domain.QueryResult.ComputerInfo =
                        queryResult.ComputerInfo
                        |> List.map computerInfoToDbDto
                }
                |> Reader.run dataStoreConfig
                logger.LogInformation("{time}: Done.", DateTimeOffset.Now)
            }
            |> fun wf -> Async.StartAsTask(wf, cancellationToken = ctx.CancellationToken) :> Task

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
            services.AddQuartz(fun quartz ->
                quartz.UseMicrosoftDependencyInjectionJobFactory()

                let jobKey = JobKey("query-computer-info", "default")
                quartz.AddJob<QueryComputerInfoJob>(jobKey) |> ignore

                quartz.AddTrigger(fun trigger ->
                    trigger
                        .ForJob(jobKey)
                        .StartNow()
                        .WithCronSchedule("0 0/30 7-21 ? * MON-FRI")
                    |> ignore
                )
                |> ignore
            )
            |> ignore

            services.AddQuartzHostedService(fun options ->
                // when shutting down we want jobs to complete gracefully
                options.WaitForJobsToComplete <- true
            )
            |> ignore
        )
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
