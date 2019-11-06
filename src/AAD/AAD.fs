module AAD

open Domain
open Microsoft.Graph
open Polly
open System
open System.IO
open System.Threading.Tasks

let private retryRequest (fn: 'a -> Task<_>) arg =
    let retryCount = 3
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
                    |> Option.defaultValue (TimeSpan.FromSeconds 2.)
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
        let ifNullEmpty v = if isNull v then "" else v
        let toSortable (v: string) =
            let a = if v.StartsWith "SMTP:" then 0 else 1
            let b = if v.EndsWith(".onmicrosoft.com", StringComparison.InvariantCultureIgnoreCase) then 1 else 0
            (a, b)
        {
            Id = UserId user.Id
            UserName = trimEMailAddressDomain user.UserPrincipalName
            FirstName = ifNullEmpty user.GivenName
            LastName = ifNullEmpty user.Surname
            MailAddresses =
                user.ProxyAddresses
                |> Seq.sortBy toSortable
                |> Seq.choose tryGetMailAddressFromProxyAddress
                |> Seq.toList
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
    inherit Group()
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

let addGroupMember (graphServiceClient: GraphServiceClient) (GroupId groupId) (UserId userId) =
    retryRequest
        (graphServiceClient.Groups.[groupId].Members.References.Request().AddAsync >> Async.AwaitTask >> Async.StartAsTask)
        (User(Id = userId))

let removeGroupMember (graphServiceClient: GraphServiceClient) (GroupId groupId) (UserId userId) =
    retryRequest
        (fun () -> graphServiceClient.Groups.[groupId].Members.[userId].Reference.Request().DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask)
        ()

let applyMemberModifications graphServiceClient groupId memberModifications =
    memberModifications
    |> List.map (function
        | AddMember userId -> addGroupMember graphServiceClient groupId userId
        | RemoveMember userId -> removeGroupMember graphServiceClient groupId userId
    )
    |> Async.Parallel
    |> Async.Ignore

let applySingleGroupModifications graphServiceClient modifications = async {
    match modifications with
    | CreateGroup (name, memberIds) ->
        let! group = createGroup graphServiceClient name
        let groupId = GroupId group.Id
        do!
            memberIds
            |> List.map AddMember
            |> applyMemberModifications graphServiceClient groupId
    | UpdateGroup (groupId, memberModifications) ->
        do! applyMemberModifications graphServiceClient groupId memberModifications
    | DeleteGroup groupId ->
        do! deleteGroup graphServiceClient groupId
}

let applyGroupsModifications graphServiceClient modifications =
    modifications
    |> List.map (applySingleGroupModifications graphServiceClient)
    |> Async.Parallel
    |> Async.Ignore

let getUserGroups (graphServiceClient: GraphServiceClient) (UserId userId) =
    readAll
        (graphServiceClient.Users.[userId].MemberOf.Request())
        (fun request -> request.GetAsync())
        (fun items -> items.NextPageRequest)

let getAutoContactIds (graphServiceClient: GraphServiceClient) (UserId userId) = async {
    let! contacts =
        readAll
            (graphServiceClient.Users.[userId].Contacts.Request().Select("id").Filter("categories/any(category: category eq 'htl-utils-auto-generated')"))
            (fun r -> r.GetAsync())
            (fun items -> items.NextPageRequest)
    return
        contacts
        |> Seq.map (fun c -> c.Id)
        |> Seq.toList
}

let removeContact (graphServiceClient: GraphServiceClient) (UserId userId) contactId =
    retryRequest
        (fun () -> graphServiceClient.Users.[userId].Contacts.[contactId].Request().DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask)
        ()

let removeAutoContacts (graphServiceClient: GraphServiceClient) userId = async {
    let! existingContactIds = getAutoContactIds graphServiceClient userId

    do!
        existingContactIds
        |> Seq.map (removeContact graphServiceClient userId)
        |> Async.Parallel
        |> Async.Ignore
}

let addContact (graphServiceClient: GraphServiceClient) (UserId userId) contact =
    retryRequest
        (graphServiceClient.Users.[userId].Contacts.Request().AddAsync)
        contact

let setContactPhoto (graphServiceClient: GraphServiceClient) (UserId userId) contactId photoStream =
    retryRequest
        (graphServiceClient.Users.[userId].Contacts.[contactId].Photo.Content.Request().PutAsync)
        photoStream

let addAutoContact (graphServiceClient: GraphServiceClient) userId contact = async {
    let! newContact =
        let birthday =
            contact.Birthday
            |> Option.map (fun date -> DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero))
            |> Option.toNullable
        let mailAddresses =
            contact.MailAddresses
            |> List.map (fun v -> EmailAddress(Address = v))
        Contact(
            GivenName = contact.FirstName,
            Surname = contact.LastName,
            DisplayName = contact.DisplayName,
            FileAs = sprintf "%s, %s" contact.LastName contact.FirstName,
            Birthday = birthday,
            HomePhones = contact.HomePhones,
            MobilePhone = Option.toObj contact.MobilePhone,
            EmailAddresses = mailAddresses,
            Categories = [ "htl-utils-auto-generated" ]
        )
        |> addContact graphServiceClient userId

    match contact.Photo with
    | Some (Base64EncodedImage photo) ->
        use stream = new MemoryStream(Convert.FromBase64String photo)
        do! setContactPhoto graphServiceClient userId newContact.Id stream |> Async.Ignore
    | None -> ()
}

let addAutoContacts (graphServiceClient: GraphServiceClient) userId contacts =
    contacts
    |> List.map (addAutoContact graphServiceClient userId)
    |> Async.Parallel
    |> Async.Ignore

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

let getBirthdayCalendarId (graphServiceClient: GraphServiceClient) = async {
    let! calendars = getCalendars graphServiceClient

    return
        calendars
        |> Seq.tryFind (fun c -> String.equalsCaseInsensitive c.Name "Birthdays")
        |> Option.map (fun c -> c.Id)
        |> Option.defaultWith (fun () ->
            let calendarNames =
                calendars
                |> Seq.map (fun c -> c.Name)
                |> String.concat ", "
            failwithf "Birthday calendar not found. Found calendars (%d): %s" (List.length calendars) calendarNames
        )
}

let getCalendarEventIds (graphServiceClient: GraphServiceClient) calendarId = async {
    let! events =
        readAll
            (graphServiceClient.Me.Calendars.[calendarId].Events.Request())
            (fun r -> r.GetAsync())
            (fun items -> items.NextPageRequest)
    return
        events
        |> Seq.map (fun e -> e.Id)
        |> Seq.toList
}

let updateCalendarEvent (graphServiceClient: GraphServiceClient) calendarId eventId updatedEvent =
    retryRequest
        (graphServiceClient.Me.Calendars.[calendarId].Events.[eventId].Request().UpdateAsync)
        updatedEvent


let turnOffBirthdayReminders (graphServiceClient: GraphServiceClient) = async {
    let! birthdayCalendarId = getBirthdayCalendarId graphServiceClient
    let! birthdayEventIds = getCalendarEventIds graphServiceClient birthdayCalendarId

    do!
        birthdayEventIds
        |> Seq.map (fun eventId -> async {
            let event' = Event(IsReminderOn = Nullable<_> false)
            return! updateCalendarEvent graphServiceClient birthdayCalendarId eventId event'
        })
        |> Async.Parallel
        |> Async.Ignore
}

let updateAutoContacts graphServiceClient userId contacts = async {
    do! removeAutoContacts graphServiceClient userId
    do! addAutoContacts graphServiceClient userId contacts
    do! turnOffBirthdayReminders graphServiceClient
}
