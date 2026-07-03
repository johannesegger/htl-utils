module IndividualTests.Server.Main

open Azure.Identity
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Identity.Web
open Microsoft.IdentityModel.Logging
open System.Text.Json.Serialization
open Microsoft.Graph.Beta

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddControllers()
        .AddJsonOptions(fun options ->
            options.JsonSerializerOptions.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
        ) |> ignore

    builder.Services.AddAuthentication()
        .AddJwtBearer(fun options ->
            builder.Configuration.GetSection("Oidc").Bind(options)
        ) |> ignore

    builder.Services.AddTransient<IClaimsTransformation>(fun provider ->
        new KeycloakRolesClaimsTransformation("htl-utils")
    ) |> ignore

    builder.Services.AddAuthorization(fun v ->
        v.AddPolicy("SendLetters", fun policy ->
            policy.RequireRole("individualtests-lettersender") |> ignore
        )
    ) |> ignore

    builder.Services.AddSingleton<Sokrates.SokratesApi>(fun _ -> Sokrates.SokratesApi.FromEnvironment()) |> ignore

    builder.Services.AddSingleton<GraphServiceClient>(fun v ->
        let credential = new ClientSecretCredential(
            builder.Configuration["AAD:TenantId"],
            builder.Configuration["AAD:ClientId"],
            builder.Configuration["AAD:ClientSecret"])

        let scopes = [ "https://graph.microsoft.com/.default" ]

        new GraphServiceClient(credential, scopes)
    ) |> ignore

    builder.Services.AddSingleton<Controllers.Html.BrowserFactory>() |> ignore

    let app = builder.Build()

    if app.Environment.IsDevelopment() then IdentityModelEventSource.ShowPII <- true

    app.UseHttpsRedirection() |> ignore

    if app.Environment.IsProduction() then
        app.UseDefaultFiles() |> ignore
        app.UseStaticFiles() |> ignore

    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore
    app.MapControllers() |> ignore

    app.Run()

    0
