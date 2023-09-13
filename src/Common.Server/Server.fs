module Server

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Identity.Web

let addAADAuth (services: IServiceCollection) (config: IConfiguration) =
    services
        .AddMicrosoftGraphBeta(config.GetSection("MicrosoftGraph"))
        .AddMicrosoftIdentityWebApiAuthentication(config)
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddInMemoryTokenCaches()
    |> ignore