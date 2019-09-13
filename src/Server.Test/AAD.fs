module AAD

open Expecto
open Microsoft.Graph
open Microsoft.Graph.Auth
open Microsoft.Identity.Client
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
        Expect.all groups (fun g -> not <| String.IsNullOrEmpty g.Id) "All groups must have an id"
        Expect.all groups (fun g -> not <| String.IsNullOrEmpty g.Name) "All groups must have a name"
        let members = groups |> List.collect (fun g -> g.Members)
        Expect.all members (fun m -> not <| String.IsNullOrEmpty m.Id) "All members must have an id"
        Expect.all members (fun m -> not <| String.IsNullOrEmpty m.Name) "All members must have a name"
        Expect.all members (fun m -> not <| String.IsNullOrEmpty m.ShortName) "All members must have a short name"
    }

    testCaseAsync "Get users" <| async {
        let! authProvider = authProvider
        let graphServiceClient = GraphServiceClient(authProvider)
        let! users = AAD.getUsers graphServiceClient
        Expect.all users (fun m -> not <| String.IsNullOrEmpty m.Id) "All users must have an id"
        Expect.all users (fun m -> not <| String.IsNullOrEmpty m.Name) "All users must have a name"
    }
]
