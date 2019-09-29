module AAD

open Expecto
open Microsoft.Graph
open Microsoft.Graph.Auth
open Microsoft.Identity.Client
open Shared.AAD
open System

let publicClientApplication =
    PublicClientApplicationBuilder
        .Create(Environment.AAD.clientId)
        .WithTenantId(Environment.AAD.tenantId)
        .Build()

let authProvider = async {
    let authProvider = UsernamePasswordProvider(publicClientApplication, [| "group.readwrite.all" |])

    // Get access token
    do!
        GraphServiceClient(authProvider)
            .Me
            .Request()
            .WithUsernamePassword(Environment.AAD.username, Environment.AAD.securePassword)
            .GetAsync()
        |> Async.AwaitTask
        |> Async.Ignore

    return authProvider
}

let tests = testList "AAD" [
    testCaseAsync "Get groups" <| async {
        let! authProvider = authProvider
        let graphServiceClient = GraphServiceClient(authProvider)
        let! groups = AAD.getGrpGroups graphServiceClient
        Expect.all groups (fun g -> let (GroupId gId) = g.Id in not <| String.IsNullOrEmpty gId) "All groups must have an id"
        Expect.all groups (fun g -> not <| String.IsNullOrEmpty g.Name) "All groups must have a name"
        let members = groups |> List.collect (fun g -> g.Members)
        Expect.all members (fun m -> let (UserId mId) = m.Id in not <| String.IsNullOrEmpty mId) "All members must have an id"
        Expect.all members (fun m -> not <| String.IsNullOrEmpty m.ShortName) "All members must have a short name"
        Expect.exists members (fun m -> not <| String.IsNullOrEmpty m.FirstName) "Some members must have a first name"
        Expect.exists members (fun m -> not <| String.IsNullOrEmpty m.LastName) "Some members must have a last name"
    }

    testCaseAsync "Get users" <| async {
        let! authProvider = authProvider
        let graphServiceClient = GraphServiceClient(authProvider)
        let! users = AAD.getUsers graphServiceClient
        Expect.all users (fun m -> let (UserId mId) = m.Id in not <| String.IsNullOrEmpty mId) "All users must have an id"
        Expect.all users (fun m -> not <| String.IsNullOrEmpty m.ShortName) "All members must have a short name"
        Expect.exists users (fun m -> not <| String.IsNullOrEmpty m.FirstName) "All members must have a first name"
        Expect.exists users (fun m -> not <| String.IsNullOrEmpty m.LastName) "All members must have a last name"
    }

    ptestCaseAsync "Create group" <| async {
        let! authProvider = authProvider
        let graphServiceClient = GraphServiceClient(authProvider)
        let! group = AAD.createGroup graphServiceClient "test"
        do! AAD.deleteGroup graphServiceClient (GroupId group.Id)
        Expect.equal group.AutoSubscribeNewMembers (Nullable true) "AutoSubscribeNewMembers should be `true`"
    }
]
