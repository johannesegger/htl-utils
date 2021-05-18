module AAD.Test.Program

open AAD.Core
open AAD.Domain
open Expecto
open Microsoft.Graph
open Microsoft.Graph.Auth
open Microsoft.Identity.Client
open System
open System.Net

let config = AAD.Configuration.Config.fromEnvironment ()

let clientApp =
    PublicClientApplicationBuilder
        .Create(config.OidcConfig.AppId)
        .WithTenantId(config.OidcConfig.TenantId)
        .Build()

let aadConfig = AAD.Configuration.Config.fromEnvironment ()
let credentials = NetworkCredential(Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_USERNAME", Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_PASSWORD")
let innerAuthProvider = UsernamePasswordProvider(clientApp)

// This is slightly better than calling `WithUsernamePassword` for every graph request
let authProvider = DelegateAuthenticationProvider(fun request ->
    // https://github.com/microsoftgraph/msgraph-sdk-dotnet-auth/blob/17f66373bf7ac4006425e34aedbaba814734230b/src/Microsoft.Graph.Auth/Extensions/BaseRequestExtensions.cs#L107-L120
    let authHandlerOption = request.GetMiddlewareOption<AuthenticationHandlerOption>()
    let authenticationProviderOption =
        match authHandlerOption.AuthenticationProviderOption with
        | :? AuthenticationProviderOption as v -> v
        | _ ->
            let v = AuthenticationProviderOption()
            authHandlerOption.AuthenticationProviderOption <- v
            v
    authenticationProviderOption.UserAccount <- GraphUserAccount(Email = credentials.UserName)
    authenticationProviderOption.Password <- credentials.SecurePassword

    let v = request.GetMiddlewareOption<AuthenticationHandlerOption>()

    innerAuthProvider.AuthenticateRequestAsync(request)
)

// let clientApp =
//     ConfidentialClientApplicationBuilder
//         .Create(config.OidcConfig.AppId)
//         .WithTenantId(config.OidcConfig.TenantId)
//         .WithClientSecret(config.OidcConfig.AppSecret)
//         .Build()
// let authProvider = new ClientCredentialProvider(clientApp)

let graphServiceClient = GraphServiceClient(authProvider)

let randomGroupName () =
    sprintf "Test-%O" (Guid.NewGuid())

let tests =
    testList "Modifications" [
        testCaseAsync "Change group name" <| async {
            let groupName = randomGroupName ()
            do!
                applyGroupsModifications graphServiceClient [
                    CreateGroup (groupName, [])
                ]

            let! groupId =
                graphServiceClient.Groups.Request()
                    .WithMaxRetry(5)
                    .Filter(sprintf "displayName eq '%s'" groupName).Select("id")
                    .GetAsync()
                |> Async.AwaitTask
                |> Async.map (Seq.exactlyOne >> fun v -> v.Id)
            let groupName = sprintf "%s-new" groupName
            do!
                applyGroupsModifications graphServiceClient [
                    ChangeGroupName (GroupId groupId, groupName)
                ]

            let! group = graphServiceClient.Groups.[groupId].Request().GetAsync() |> Async.AwaitTask
            do! graphServiceClient.Groups.[groupId].Request().DeleteAsync() |> Async.AwaitTask

            Expect.all (group.Mail :: Seq.toList group.ProxyAddresses) (fun address -> address.Contains(groupName)) "Mail addresses should contain new group name"
        }
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args tests
