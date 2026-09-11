namespace Managementv2.Server.Controllers

open System
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Managementv2.Server

[<ApiController>]
[<Route("[controller]")>]
type WeatherForecastController(_logger: ILogger<WeatherForecastController>) =
    inherit ControllerBase()

    let summaries =
        [| "Freezing"
           "Bracing"
           "Chilly"
           "Cool"
           "Mild"
           "Warm"
           "Balmy"
           "Hot"
           "Sweltering"
           "Scorching" |]

    [<HttpGet>]
    member _.Get() =
        let rng = Random()

        [| for index in 0..4 ->
               { Date = DateTime.Now.AddDays(float index)
                 TemperatureC = rng.Next(-20, 55)
                 Summary = summaries.[rng.Next(summaries.Length)] } |]