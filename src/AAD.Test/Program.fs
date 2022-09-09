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

let getGroup groupName = async {
    let! groups =
        graphServiceClient.Groups.Request()
            .WithMaxRetry(5)
            .Filter(sprintf "displayName eq '%s'" groupName)
            .Select("id,displayName,mail")
            .GetAsync()
        |> Async.AwaitTask
    return groups |> Seq.exactlyOne
}

let tests =
    testList "Modifications" [
        testCaseAsync "Create group" <| async {
            let groupName = randomGroupName ()
            do!
                applyGroupsModifications graphServiceClient [
                    CreateGroup (groupName, [])
                ]

            let! group = getGroup groupName
            do! graphServiceClient.Groups.[group.Id].Request().DeleteAsync() |> Async.AwaitTask
        }

        ftestCaseAsync "Create group with member" <| async {
            let groupName = randomGroupName ()
            let! me =
                graphServiceClient.Me.Request()
                    .WithMaxRetry(5)
                    .GetAsync()
                |> Async.AwaitTask
            let! memberIds =
                graphServiceClient.Users.Request()
                    .WithMaxRetry(5)
                    .Filter("userPrincipalName eq 'eggj@htlvb.at'").Select("id")
                    .GetAsync()
                |> Async.AwaitTask
                |> Async.map (Seq.map (fun v -> UserId v.Id) >> Seq.toList)
            do!
                applyGroupsModifications graphServiceClient [
                    CreateGroup (groupName, memberIds)
                ]

            let! group = getGroup groupName

            do! Async.Sleep (TimeSpan.FromMinutes 1.)

            let message =
                Message(
                    Subject = "Test",
                    Body = ItemBody(
                        ContentType = BodyType.Text,
                        Content = "Test"
                    ),
                    From = Recipient(EmailAddress = EmailAddress(Address = me.Mail)),
                    ToRecipients = [
                        Recipient(EmailAddress = EmailAddress(Address = group.Mail))
                    ]
                )
            do! graphServiceClient.Me.SendMail(message, SaveToSentItems = Nullable false).Request().PostAsync() |> Async.AwaitTask

            do! graphServiceClient.Groups.[group.Id].Request().DeleteAsync() |> Async.AwaitTask

            // TODO check that no welcome mail is received and that you received the test mail in your inbox
        }

        ptestCaseAsync "Change group name" <| async {
            let groupName = randomGroupName ()
            do!
                applyGroupsModifications graphServiceClient [
                    CreateGroup (groupName, [])
                ]

            let! group = getGroup groupName
            let groupName = sprintf "%s-new" groupName
            do!
                applyGroupsModifications graphServiceClient [
                    ChangeGroupName (GroupId group.Id, groupName)
                ]

            let! group = getGroup groupName
            do! graphServiceClient.Groups.[group.Id].Request().DeleteAsync() |> Async.AwaitTask

            Expect.all (group.Mail :: Seq.toList group.ProxyAddresses) (fun address -> address.Contains(groupName)) "Mail addresses should contain new group name"
        }
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args tests
