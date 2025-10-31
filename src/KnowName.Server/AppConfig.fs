namespace KnowName.Server

open System
open System.IO
open System.Text.Json

type AppConfig = {
    Sokrates: {|
        WebServiceUrl: string
        SchoolId: string
        UserName: string
        Password: string
        ClientCertificate: byte[]
    |}
}
module AppConfig =
    open System.Security.Cryptography.X509Certificates

    let private isNonEmpty = String.IsNullOrEmpty >> not

    let private isValidCertificate (content: byte[]) =
        content.Length > 0

    let isValid config =
        isNonEmpty config.Sokrates.WebServiceUrl &&
        isNonEmpty config.Sokrates.SchoolId &&
        isNonEmpty config.Sokrates.UserName &&
        isNonEmpty config.Sokrates.Password &&
        isValidCertificate config.Sokrates.ClientCertificate

type AppConfigUpdate = {
    Sokrates: {|
        WebServiceUrl: string option
        SchoolId: string option
        UserName: string option
        Password: string option
        ClientCertificate: byte[] option
    |}
}

module AppConfigUpdate =
    let tryApply (config: AppConfig) (update: AppConfigUpdate) =
        let newConfig =
            { config with
                Sokrates = {|
                    config.Sokrates with
                        WebServiceUrl = update.Sokrates.WebServiceUrl |> Option.defaultValue config.Sokrates.WebServiceUrl
                        SchoolId = update.Sokrates.SchoolId |> Option.defaultValue config.Sokrates.SchoolId
                        UserName = update.Sokrates.UserName |> Option.defaultValue config.Sokrates.UserName
                        Password = update.Sokrates.Password |> Option.defaultValue config.Sokrates.Password
                        ClientCertificate = update.Sokrates.ClientCertificate |> Option.defaultValue config.Sokrates.ClientCertificate
                |}
            }
        if AppConfig.isValid newConfig then Some newConfig
        else None

    let tryConvertToConfig (update: AppConfigUpdate) =
        let config: AppConfig = {
            Sokrates = {|
                WebServiceUrl = update.Sokrates.WebServiceUrl |> Option.defaultValue ""
                SchoolId = update.Sokrates.SchoolId |> Option.defaultValue ""
                UserName = update.Sokrates.UserName |> Option.defaultValue ""
                Password = update.Sokrates.Password |> Option.defaultValue ""
                ClientCertificate = update.Sokrates.ClientCertificate |> Option.defaultValue [||]
            |}
        }
        if AppConfig.isValid config then Some config
        else None

type AppConfigStorage(path: string) =
    let jsonConfig = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)

    member _.TryReadConfig() : AppConfig option =
        try
            use stream = File.OpenRead path
            JsonSerializer.Deserialize<AppConfig>(stream, jsonConfig)
            |> Some
        with :? FileNotFoundException -> None

    member _.WriteConfig(config: AppConfig) : unit =
        use stream = File.OpenWrite path
        JsonSerializer.Serialize(stream, config, jsonConfig)
