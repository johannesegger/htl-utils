module Server

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Identity.Web

let addAADAuth (services: IServiceCollection) (config: IConfiguration) =
    services
        .AddMicrosoftIdentityWebApiAuthentication(config)
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddInMemoryTokenCaches()
        .AddMicrosoftGraph(config.GetSection("MicrosoftGraph"))
    |> ignore