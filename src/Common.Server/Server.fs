module Server

open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.Extensions.DependencyInjection

let addAADAuth (services: IServiceCollection) clientId authority =
    services
        .AddAuthentication(fun config ->
            config.DefaultScheme <- JwtBearerDefaults.AuthenticationScheme
            config.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(fun config ->
            config.Audience <- clientId
            config.Authority <- authority
            config.TokenValidationParameters.ValidateIssuer <- false
            config.TokenValidationParameters.SaveSigninToken <- true
        ) |> ignore
