[<AutoOpen>]
module AAD.MicrosoftGraphExtensions

open Azure.Core
open Azure.Identity
open Microsoft.Graph.Beta
open Microsoft.Kiota.Abstractions.Serialization
open System
open System.IO
open System.Threading.Tasks

module GraphServiceClientFactory =
    let createWithAppSecret (config: Configuration.OidcConfig) =
        let tokenCredential = ClientSecretCredential(
            config.TenantId,
            config.AppId,
            config.AppSecret
        )
        new GraphServiceClient(tokenCredential, [| "https://graph.microsoft.com/.default" |])

    let createWithDeviceCode (config: Configuration.OidcConfig) scopes tokenCacheName =
        let tokenCredential =
            let opts = DeviceCodeCredentialOptions (
                ClientId = config.AppId,
                TenantId = config.TenantId,
                TokenCachePersistenceOptions = TokenCachePersistenceOptions(Name = tokenCacheName),
                DeviceCodeCallback = (fun code ct ->
                    Console.ForegroundColor <- ConsoleColor.Yellow
                    printfn $"%s{code.Message}"
                    Console.ResetColor()
                    Task.CompletedTask
                )
            )
            let authTokenPath = Path.Combine(Path.GetTempPath(), $"%s{opts.TokenCachePersistenceOptions.Name}.token")
            if File.Exists(authTokenPath) then
                use fileStream = File.OpenRead(authTokenPath)
                opts.AuthenticationRecord <- AuthenticationRecord.DeserializeAsync(fileStream) |> Async.AwaitTask |> Async.RunSynchronously
                DeviceCodeCredential(opts)
            else
                let deviceCodeCredential = DeviceCodeCredential(opts)
                let authenticationRecord = deviceCodeCredential.AuthenticateAsync(TokenRequestContext(scopes)) |> Async.AwaitTask |> Async.RunSynchronously
                use fileStream = File.OpenWrite(authTokenPath)
                authenticationRecord.SerializeAsync(fileStream) |> Async.AwaitTask |> Async.RunSynchronously
                deviceCodeCredential
        new GraphServiceClient(tokenCredential, scopes)

module GraphServiceClient =
    let formatError errorTitle wf = async {
        try
            return! wf
        with
            | :? AggregateException as e ->
                match e.InnerException with
                | :? Models.ODataErrors.ODataError as e ->
                    return failwith $"%s{errorTitle}: %s{e.Error.Message}"
                | _ -> return raise e.InnerException
            | e -> return raise e
    }

[<AutoOpen>]
module TypeExtensions =
    type GraphServiceClient with
        member this.ReadAll<'a, 'b when 'a: (new: unit -> 'a) and 'a :> IParsable and 'a :> IAdditionalDataHolder> (query: Task<'a>) = async {
            let result = Collections.Generic.List<_>()
            let! firstResponse = query |> Async.AwaitTask
            let iterator =
                Microsoft.Graph.PageIterator<'b, 'a>
                    .CreatePageIterator(
                        this,
                        firstResponse,
                        (fun item ->
                            result.Add(item)
                            true // continue iteration
                        )
                    )
            do! iterator.IterateAsync() |> Async.AwaitTask
            return result
        }

        member this.GetDirectoryObjectReference (obj: Models.Group) =
            Models.ReferenceCreate(OdataId = $"{this.RequestAdapter.BaseUrl}/groups/{obj.Id}")

        member this.GetDirectoryObjectReference (obj: Models.User) =
            Models.ReferenceCreate(OdataId = $"{this.RequestAdapter.BaseUrl}/users/{obj.Id}")
