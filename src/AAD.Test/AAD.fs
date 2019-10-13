module AAD

open Expecto
open Microsoft.Graph
open Microsoft.Graph.Auth
open Microsoft.Identity.Client
open Shared
open System

let private clientApp =
    ConfidentialClientApplicationBuilder
        .Create(Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_CLIENT_ID")
        .WithTenantId(Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_TENANT_ID")
        .WithClientSecret(Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_APP_KEY")
        .Build()

let getGraphServiceClient () =
    ClientCredentialProvider(clientApp)
    |> GraphServiceClient

let tests = testList "AAD" [
    testCaseAsync "Get auto groups" <| async {
        let graphServiceClient = getGraphServiceClient ()
        let! groups = AAD.getAutoGroups graphServiceClient
        Expect.all groups (fun g -> let (GroupId gId) = g.Id in not <| String.IsNullOrEmpty gId) "All groups must have an id"
        Expect.all groups (fun g -> not <| String.IsNullOrEmpty g.Name) "All groups must have a name"
        Expect.all groups (fun g -> not <| String.IsNullOrEmpty g.Mail) "All groups must have a mail"
        let memberIds = groups |> List.collect (fun g -> g.Members)
        Expect.all memberIds (fun (UserId userId) -> not <| String.IsNullOrEmpty userId) "All members must have an id"
    }

    testCaseAsync "Get users" <| async {
        let graphServiceClient = getGraphServiceClient ()
        let! users = AAD.getUsers graphServiceClient
        Expect.all users (fun m -> let (UserId mId) = m.Id in not <| String.IsNullOrEmpty mId) "All users must have an id"
        Expect.all users (fun m -> not <| String.IsNullOrEmpty m.ShortName) "All members must have a short name"
        Expect.exists users (fun m -> not <| String.IsNullOrEmpty m.FirstName) "Some members must have a first name"
        Expect.exists users (fun m -> not <| String.IsNullOrEmpty m.LastName) "Some members must have a last name"
        Expect.all users (fun m -> not <| List.isEmpty m.MailAddresses) "All members must have at least one mail address"
    }

    ptestCaseAsync "Create group" <| async {
        let graphServiceClient = getGraphServiceClient ()
        let! group = AAD.createGroup graphServiceClient "test"
        do! AAD.deleteGroup graphServiceClient (GroupId group.Id)
        Expect.equal group.AutoSubscribeNewMembers (Nullable true) "AutoSubscribeNewMembers should be `true`"
    }
]
