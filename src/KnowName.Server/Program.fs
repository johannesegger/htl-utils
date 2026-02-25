module KnowName.Server.Program

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open System
open Microsoft.AspNetCore.Authentication

[<EntryPoint>]
let main args =

    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddControllers() |> ignore

    builder.Services.AddAuthentication()
        .AddJwtBearer(fun options ->
            builder.Configuration.GetSection("Oidc").Bind(options)
        ) |> ignore
    builder.Services.AddTransient<IClaimsTransformation>(fun provider ->
        new KeycloakRolesClaimsTransformation("htl-utils")
    ) |> ignore

    builder.Services.AddAuthorization(fun v ->
        v.AddPolicy("ReadPersonData", fun policy ->
            policy.RequireRole("knowname-user") |> ignore
        )
    ) |> ignore

    builder.Services.AddAuthorization(fun v ->
        v.AddPolicy("ManageSettings", fun policy ->
            policy.RequireRole("knowname-admin") |> ignore
        )
    ) |> ignore

    builder.Services.AddSingleton(AppConfigStorage(builder.Configuration.GetValue<string>("AppConfigPath"))) |> ignore
    builder.Services.AddScoped<Sokrates.Config>(fun (ctx: IServiceProvider) ->
        let appConfigStorage = ctx.GetRequiredService<AppConfigStorage>()
        match appConfigStorage.TryReadConfig() with
        | Some config ->
            let sokratesConfig : Sokrates.Config = {
                WebServiceUrl = config.Sokrates.WebServiceUrl
                UserName = config.Sokrates.UserName
                Password = config.Sokrates.Password
                SchoolId = config.Sokrates.SchoolId
                ClientCertificate = config.Sokrates.ClientCertificate
                ClientCertificatePassphrase = ""
            }
            sokratesConfig
        | None ->
            let sokratesConfig : Sokrates.Config = {
                WebServiceUrl = ""
                UserName = ""
                Password = ""
                SchoolId = ""
                ClientCertificate = [||]
                ClientCertificatePassphrase = ""
            }
            sokratesConfig
    ) |> ignore
    builder.Services.AddScoped<Sokrates.SokratesApi>() |> ignore
    builder.Services.AddSingleton(PhotoLibrary.Configuration.Config.fromEnvironment()) |> ignore

    let app = builder.Build()

    app.UseHttpsRedirection() |> ignore

    if app.Environment.IsProduction() then
        app.UseDefaultFiles() |> ignore
        app.UseStaticFiles() |> ignore
        app.MapFallbackToFile("/index.html") |> ignore

    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore
    app.MapControllers() |> ignore

    app.Run()

    0
