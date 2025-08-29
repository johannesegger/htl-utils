module IndividualTests.Server.Main

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Identity.Web
open Microsoft.IdentityModel.Logging
open System.Text.Json.Serialization

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddControllers()
        .AddJsonOptions(fun options ->
            options.JsonSerializerOptions.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
        ) |> ignore

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddMicrosoftGraph(builder.Configuration.GetSection("GraphBeta"))
        .AddInMemoryTokenCaches() |> ignore

    let app = builder.Build()

    if app.Environment.IsDevelopment() then IdentityModelEventSource.ShowPII <- true

    app.UseHttpsRedirection() |> ignore

    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore
    app.MapControllers() |> ignore

    app.Run()

    0
