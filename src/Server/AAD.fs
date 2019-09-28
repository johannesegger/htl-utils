module AAD

open Microsoft.Graph
open Polly
open Shared.AAD
open System
open System.Threading.Tasks

let private retryRequest (fn: 'a -> Task<_>) arg =
    Policy
        .HandleInner<ServiceException>()
        .WaitAndRetryAsync(
            6,
            Func<_, _, _, _>(fun (i: int) (ex: exn) ctx ->
                let timeout =
                    ex :?> ServiceException |> Option.ofObj
                    |> Option.bind (fun p -> p.ResponseHeaders |> Option.ofObj)
                    |> Option.bind (fun p -> p.RetryAfter |> Option.ofObj)
                    |> Option.bind (fun p -> p.Delta |> Option.ofNullable)
                    |> Option.defaultValue (TimeSpan.FromSeconds (pown 2. i))
                printfn "Warning: Request #%d/6 failed. Waiting %O before retrying. %s" i timeout ex.Message
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

let getContactIds (graphServiceClient: GraphServiceClient) = async {
    let! contacts =
        readAll
            (graphServiceClient.Me.Contacts.Request().Select("id"))
            (fun r -> r.GetAsync())
            (fun items -> items.NextPageRequest)
    return
        contacts
        |> Seq.map (fun c -> c.Id)
        |> Seq.toList
}

let removeContact (graphServiceClient: GraphServiceClient) contactId =
    retryRequest
        (fun () -> graphServiceClient.Me.Contacts.[contactId].Request().DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask)
        ()

let addContact (graphServiceClient: GraphServiceClient) contact =
    retryRequest
        (graphServiceClient.Me.Contacts.Request().AddAsync)
        contact

let setContactPhoto (graphServiceClient: GraphServiceClient) contactId photoStream =
    retryRequest
        (graphServiceClient.Me.Contacts.[contactId].Photo.Content.Request().PutAsync)
        photoStream

type Calendar = {
    Id: string
    Name: string
}

let getCalendars (graphServiceClient: GraphServiceClient) = async {
    let! calendars =
        retryRequest
            (fun () -> graphServiceClient.Me.Calendars.Request().Select("id,name").GetAsync())
            ()
    return
        calendars
        |> Seq.map (fun c -> { Id = c.Id; Name = c.Name })
        |> Seq.toList
}

type CalendarEvent = {
    Id: string
}

let getCalendarEvents (graphServiceClient: GraphServiceClient) calendarId = async {
    let! events =
        readAll
            (graphServiceClient.Me.Calendars.[calendarId].Events.Request())
            (fun r -> r.GetAsync())
            (fun items -> items.NextPageRequest)
    return
        events
        |> Seq.map (fun e -> { Id = e.Id })
        |> Seq.toList
}

let updateCalendarEvent (graphServiceClient: GraphServiceClient) calendarId eventId updatedEvent =
    retryRequest
        (graphServiceClient.Me.Calendars.[calendarId].Events.[eventId].Request().UpdateAsync)
        updatedEvent

let trimEMailAddressDomain (address: string) =
    match address.IndexOf '@' with
    | -1 -> address
    | i -> address.Substring(0, i)

type User = {
    Id: UserId
    ShortName: string
    FirstName: string
    LastName: string
    MailAddresses: string list
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

type Group = {
    Id: GroupId
    Name: string
    Mail: string
    Members: User list
}

let loadGroups (graphServiceClient: GraphServiceClient) graphGroups =
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
                    |> List.map (fun m -> m :?> Microsoft.Graph.User |> User.toDomain)
            }
    })
    |> Async.Parallel
    |> Async.map Array.toList

let getGroups (graphServiceClient: GraphServiceClient) = async {
    let! graphGroups =
        readAll
            (graphServiceClient.Groups.Request()
                .Filter("startsWith(mail,'GrpLehrer')")
                .Select("id,displayName,mail"))
            (fun r -> r.GetAsync())
            (fun items -> items.NextPageRequest)
    return! loadGroups graphServiceClient graphGroups
}

let getGrpGroups (graphServiceClient: GraphServiceClient) = async {
    let! graphGroups =
        readAll
            (graphServiceClient.Groups.Request()
                .Filter("startsWith(displayName,'Grp')")
                .Select("id,displayName,mail"))
            (fun r -> r.GetAsync())
            (fun items -> items.NextPageRequest)

    return! loadGroups graphServiceClient graphGroups
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

let createGroup (graphServiceClient: GraphServiceClient) name = async {
    let group =
        Group(
            DisplayName = name,
            MailEnabled = Nullable true,
            MailNickname = name,
            SecurityEnabled = Nullable true,
            GroupTypes = [ "Unified" ],
            Visibility = "Private"
        )
    return! retryRequest (graphServiceClient.Groups.Request().AddAsync) group
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
