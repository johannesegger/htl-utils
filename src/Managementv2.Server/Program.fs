namespace Managementv2.Server

#nowarn "20"

open System.IO
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Builder
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

        builder.Services.AddAuthentication()
            .AddJwtBearer(fun options ->
                builder.Configuration.GetSection("Oidc").Bind(options)
            ) |> ignore
        builder.Services.AddTransient<IClaimsTransformation>(fun provider ->
            new KeycloakRolesClaimsTransformation("htl-utils")
        ) |> ignore

        builder.Services.AddAuthorization(fun v ->
            v.AddPolicy("ExecuteCustomOperations", fun policy ->
                policy.RequireRole("itmgmt-custom-operation-executor") |> ignore
            )
            v.AddPolicy("ManageCustomOperations", fun policy ->
                policy.RequireRole("itmgmt-custom-operation-manager") |> ignore
            )
        ) |> ignore

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

        app.UseAuthentication()
        app.UseAuthorization()
        app.MapControllers()

        app.Run()

        exitCode