module ManageGuestAccounts.Server.Program

open AD.Configuration
open AD.Core
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Identity.Web
open System.Text.Json.Serialization

[<EntryPoint>]
let main args =

    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddControllers()
        .AddJsonOptions(fun options ->
            options.JsonSerializerOptions.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
        ) |> ignore

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AAD"))
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddMicrosoftGraph(builder.Configuration.GetSection("Graph"))
        .AddInMemoryTokenCaches() |> ignore

    builder.Services.AddAuthorization(fun v ->
        v.AddPolicy("ManageGuestAccounts", fun policy ->
            policy.RequireRole("GuestAccounts.Manager") |> ignore
        )
    ) |> ignore

    builder.Services.AddControllers() |> ignore

    builder.Services.AddTransient<ADApi>(fun ctx ->
        new ADApi(Config.fromEnvironment())
    ) |> ignore

    let app = builder.Build()

    app.UseHttpsRedirection() |> ignore

    if app.Environment.IsProduction() then
        app.UseDefaultFiles() |> ignore
        app.UseStaticFiles() |> ignore

    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore
    app.MapControllers() |> ignore

    app.Run()

    0
