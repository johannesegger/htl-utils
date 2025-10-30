module KnowName.Server.Program

open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Identity.Web

[<EntryPoint>]
let main args =

    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddControllers() |> ignore

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AAD"))
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddMicrosoftGraph(builder.Configuration.GetSection("Graph"))
        .AddInMemoryTokenCaches() |> ignore

    builder.Services.AddAuthorization(fun v ->
        v.AddPolicy("ReadPersonData", fun policy ->
            policy.RequireRole("KnowName.User") |> ignore
        )
    ) |> ignore

    builder.Services.AddAuthorization(fun v ->
        v.AddPolicy("ManageSettings", fun policy ->
            policy.RequireRole("KnowName.Admin") |> ignore
        )
    ) |> ignore

    builder.Services.AddSingleton(Sokrates.Config.fromEnvironment ()) |> ignore
    builder.Services.AddSingleton<Sokrates.SokratesApi>() |> ignore
    builder.Services.AddSingleton(PhotoLibrary.Configuration.Config.fromEnvironment()) |> ignore

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
