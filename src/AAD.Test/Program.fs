module AAD.Test.Program

open AAD.Core
open AAD.Domain
open Azure.Identity
open Expecto
open Microsoft.Graph.Beta
open Microsoft.Identity.Client
open Microsoft.Kiota.Abstractions
open Microsoft.Kiota.Http.HttpClientLibrary.Middleware.Options
open System
open System.Net

let config = AAD.Configuration.Config.fromEnvironment ()

let userName = Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_USERNAME"
let password = Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_PASSWORD"
let authProvider = UsernamePasswordCredential(userName, password, config.OidcConfig.TenantId, config.OidcConfig.AppId)
let graphServiceClient = new GraphServiceClient(authProvider)

let randomGroupName () =
    sprintf "Test-%O" (Guid.NewGuid())

let getGroup groupName = async {
    let! groups =
        graphServiceClient.Groups.GetAsync(fun config ->
            config.QueryParameters.Filter <- $"displayName eq '%s{groupName}'"
            config.QueryParameters.Select <- [| "id"; "displayName"; "mail" |]
        )
        |> Async.AwaitTask
    return groups.Value |> Seq.exactlyOne
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
            do! graphServiceClient.Groups.[group.Id].DeleteAsync() |> Async.AwaitTask
        }

        testCaseAsync "Create group with member" <| async {
            let groupName = randomGroupName ()
            let! me = graphServiceClient.Me.GetAsync() |> Async.AwaitTask
            let! memberIds = async {
                let! response =
                    graphServiceClient.Users.GetAsync(fun config ->
                        config.QueryParameters.Filter <- "userPrincipalName eq 'eggj@htlvb.at'"
                        config.QueryParameters.Select <- [| "id" |]
                    )
                    |> Async.AwaitTask
                return response.Value |> Seq.map (fun v -> UserId v.Id) |> Seq.toList
            }
            do!
                applyGroupsModifications graphServiceClient [
                    CreateGroup (groupName, memberIds)
                ]

            let! group = getGroup groupName

            let message =
                Models.Message(
                    Subject = "Test",
                    Body = Models.ItemBody(
                        ContentType = Models.BodyType.Text,
                        Content = "Test"
                    ),
                    From = Models.Recipient(EmailAddress = Models.EmailAddress(Address = me.Mail)),
                    ToRecipients = Collections.Generic.List [
                        Models.Recipient(EmailAddress = Models.EmailAddress(Address = group.Mail))
                    ]
                )
            let body = Me.SendMail.SendMailPostRequestBody(Message = message, SaveToSentItems = Nullable false)
            do! graphServiceClient.Me.SendMail.PostAsync(body) |> Async.AwaitTask

            do! graphServiceClient.Groups.[group.Id].DeleteAsync() |> Async.AwaitTask

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
            do! graphServiceClient.Groups.[group.Id].DeleteAsync() |> Async.AwaitTask

            Expect.all (group.Mail :: Seq.toList group.ProxyAddresses) (fun address -> address.Contains(groupName)) "Mail addresses should contain new group name"
        }
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args tests
