module AAD

open Microsoft.Graph
open Polly
open Shared
open System
open System.Threading.Tasks

let private retryRequest (fn: 'a -> Task<_>) arg =
    let retryCount = 5
    Policy
        .HandleInner<ServiceException>()
        .WaitAndRetryAsync(
            retryCount,
            Func<_, _, _, _>(fun (i: int) (ex: exn) ctx ->
                let timeout =
                    ex :?> ServiceException |> Option.ofObj
                    |> Option.bind (fun p -> p.ResponseHeaders |> Option.ofObj)
                    |> Option.bind (fun p -> p.RetryAfter |> Option.ofObj)
                    |> Option.bind (fun p -> p.Delta |> Option.ofNullable)
                    |> Option.defaultValue (TimeSpan.FromSeconds (pown 2. i))
                printfn "Warning: Request #%d/%d failed. Waiting %O before retrying. %s" i retryCount timeout ex.Message
                timeout
            ),
            Func<_, _, _, _, _>(fun ex t i ctx -> Task.CompletedTask))
        .ExecuteAsync(fun () -> fn arg)
    |> Async.AwaitTask

let rec private readRemaining (initialItems: 'items) (getNextRequest: 'items -> 'req) (getItems: 'req -> Task<'items>) =
    let rec fetchNextItems currentItems allItems = async {
        match getNextRequest currentItems |> Option.ofObj with
        | Some request ->
            let! nextItems = retryRequest getItems request
            return!
                nextItems
                |> Seq.toList
                |> List.append allItems
                |> fetchNextItems nextItems
        | None -> return allItems
    }

    fetchNextItems initialItems (Seq.toList initialItems)

let rec private readAll (initialRequest: 'req) (getItems: 'req -> Task<'items>) (getNextRequest: 'items -> 'req) = async {
    let! initialItems = retryRequest getItems initialRequest
    return! readRemaining initialItems getNextRequest getItems
}

module User =
    let internal fields = "id,userPrincipalName,givenName,surname,proxyAddresses"
    let toDomain (user: Microsoft.Graph.User) =
        let tryGetMailAddressFromProxyAddress (proxyAddress: string) =
            let prefix = "smtp:"
            if String.startsWithCaseInsensitive prefix proxyAddress
            then Some (proxyAddress.Substring prefix.Length)
            else None
        {
            Id = UserId user.Id
            ShortName = trimEMailAddressDomain user.UserPrincipalName
            FirstName = user.GivenName
            LastName = user.Surname
            MailAddresses = user.ProxyAddresses |> Seq.choose tryGetMailAddressFromProxyAddress |> Seq.toList
        }

let getAutoGroups (graphServiceClient: GraphServiceClient) = async {
    let! graphGroups =
        readAll
            (graphServiceClient.Groups.Request()
                .Filter("startsWith(displayName,'Grp')")
                .Select("id,displayName,mail"))
            (fun r -> r.GetAsync())
            (fun items -> items.NextPageRequest)
    return!
        graphGroups
        |> Seq.map (fun (g: Microsoft.Graph.Group) -> async {
            let! members =
                readAll
                    (graphServiceClient.Groups.[g.Id].Members.Request().Select(User.fields))
                    (fun r -> r.GetAsync())
                    (fun items -> items.NextPageRequest)
            return
                {
                    Id = GroupId g.Id
                    Name = g.DisplayName
                    Mail = g.Mail
                    Members =
                        members
                        |> List.map (fun m -> (m :?> Microsoft.Graph.User).Id |> UserId)
                }
        })
        |> Async.Parallel
        |> Async.map Array.toList
}

let getUsers (graphServiceClient: GraphServiceClient) = async {
    let! users =
        readAll
            (graphServiceClient.Users.Request().Select(User.fields))
            (fun r -> r.GetAsync())
            (fun items -> items.NextPageRequest)
    return
        users
        |> List.map User.toDomain
}

// see https://github.com/microsoftgraph/msgraph-sdk-dotnet/issues/528#issuecomment-523083170
type ExtendedGroup() =
    inherit Microsoft.Graph.Group()
        [<Newtonsoft.Json.JsonProperty(DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore, PropertyName = "resourceBehaviorOptions")>]
        member val ResourceBehaviorOptions = [||] with get, set

let createGroup (graphServiceClient: GraphServiceClient) name = async {
    let group =
        ExtendedGroup(
            DisplayName = name,
            MailEnabled = Nullable true,
            MailNickname = name,
            SecurityEnabled = Nullable true,
            GroupTypes = [ "Unified" ],
            Visibility = "Private",
            ResourceBehaviorOptions = [| "WelcomeEmailDisabled" |]
        )
    let! group = retryRequest (graphServiceClient.Groups.Request().AddAsync) group
    let groupUpdate = Group(AutoSubscribeNewMembers = Nullable true)
    return! retryRequest (graphServiceClient.Groups.[group.Id].Request().UpdateAsync) groupUpdate
}

let deleteGroup (graphServiceClient: GraphServiceClient) (GroupId groupId) =
    retryRequest (fun () -> graphServiceClient.Groups.[groupId].Request().DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask) ()

let addMembersToGroup (graphServiceClient: GraphServiceClient) (GroupId groupId) memberIds =
    memberIds
    |> List.map (fun (UserId memberId) ->
        retryRequest
            (graphServiceClient.Groups.[groupId].Members.References.Request().AddAsync >> Async.AwaitTask >> Async.StartAsTask)
            (User(Id = memberId))
    )
    |> Async.Parallel
    |> Async.Ignore

let removeMembersFromGroup (graphServiceClient: GraphServiceClient) (GroupId groupId) memberIds =
    memberIds
    |> List.map (fun (UserId memberId) ->
        retryRequest
            (fun () -> graphServiceClient.Groups.[groupId].Members.[memberId].Reference.Request().DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask)
            ()
    )
    |> Async.Parallel
    |> Async.Ignore
