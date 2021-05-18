module Server

open Microsoft.Extensions.DependencyInjection
open Microsoft.Identity.Web

let addAADAuth (services: IServiceCollection) config =
    services.AddMicrosoftIdentityWebApiAuthentication(config)
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddMicrosoftGraph(config.GetSection("MicrosoftGraph"))
        .AddInMemoryTokenCaches()
    |> ignore