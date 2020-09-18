module AAD.Test.Program

open AAD.Core
open AAD.Domain
open Expecto
open Microsoft.Graph
open Microsoft.Graph.Auth
open Microsoft.Identity.Client
open System
open System.Net

let clientApp =
    PublicClientApplicationBuilder
        .Create(Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_CLIENT_ID")
        .WithTenantId(Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_TENANT_ID")
        .Build()

let authProvider = UsernamePasswordProvider(clientApp)

let graphServiceClient = GraphServiceClient(authProvider)

let credentials = NetworkCredential(Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_USERNAME", Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_PASSWORD")
let graphClient = { Client = graphServiceClient; Authentication = UserNamePassword (credentials.UserName, credentials.SecurePassword) }

let randomGroupName () =
    sprintf "Test-%O" (Guid.NewGuid())

let tests =
    testList "Modifications" [
        ptestCaseAsync "Change group name" <| async {
            let groupName = randomGroupName ()
            do!
                applyGroupsModifications graphClient [
                    CreateGroup (groupName, [])
                ]

            let! groupId =
                graphClient.Client.Groups.Request()
                    .WithMaxRetry(5)
                    .Filter(sprintf "displayName eq '%s'" groupName).Select("id")
                    .GetAsync()
                |> Async.AwaitTask
                |> Async.map (Seq.exactlyOne >> fun v -> v.Id)
            let groupName = sprintf "%s-new" groupName
            do!
                applyGroupsModifications graphClient [
                    ChangeGroupName (GroupId groupId, groupName)
                ]

            let! group = graphClient.Client.Groups.[groupId].Request().GetAsync() |> Async.AwaitTask
            do! graphClient.Client.Groups.[groupId].Request().DeleteAsync() |> Async.AwaitTask

            Expect.all (group.Mail :: Seq.toList group.ProxyAddresses) (fun address -> address.Contains(groupName)) "Mail addresses should contain new group name"
        }
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args tests
