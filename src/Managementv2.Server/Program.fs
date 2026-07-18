namespace Managementv2.Server

#nowarn "20"

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.HttpsPolicy
open Microsoft.AspNetCore.Mvc.Formatters
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

module Program =
    let exitCode = 0

    [<EntryPoint>]
    let main args =

        let builder = WebApplication.CreateBuilder(args)

        builder.Services.AddControllers(fun opt ->
            opt.OutputFormatters.RemoveType<HttpNoContentOutputFormatter>()
        )

        let customOperationsDirectory =
            builder.Configuration.GetValue<string> "CustomOperationsDirectory"
            |> Option.ofObj
            |> Option.defaultValue "."

        builder.Services.AddSingleton<CodeExecution>()

        builder.Services.AddSingleton<ICustomOperationsConfig>(
            JsonFileCustomOperationsConfig(Path.Combine(customOperationsDirectory, "config.json"))
            :> ICustomOperationsConfig
        )

        builder.Services.AddSingleton<ICustomOperationsStore>(fun ctx ->
            let logger = ctx.GetRequiredService<ILogger<FileSystemCustomOperationsStore>>()
            FileSystemCustomOperationsStore(customOperationsDirectory, logger) :> ICustomOperationsStore
        )

        let app = builder.Build()

        app.UseHttpsRedirection()

        app.UseAuthorization()
        app.MapControllers()

        app.Run()

        exitCode