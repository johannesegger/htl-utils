module Server

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Identity.Web

let addAADAuth (services: IServiceCollection) (config: IConfiguration) =
    services
        .AddMicrosoftGraphBeta(fun options ->
            options.BaseUrl <- "https://graph.microsoft.com/beta"
        )
        .AddMicrosoftIdentityWebApiAuthentication(config)
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddInMemoryTokenCaches()
    |> ignore