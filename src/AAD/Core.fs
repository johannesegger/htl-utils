module AAD.Core

open AAD.Domain
open Microsoft.Graph
open Microsoft.Graph.Auth
open Microsoft.Identity.Client
open Polly
open System
open System.IO
open System.Threading.Tasks

let private clientApp =
    ConfidentialClientApplicationBuilder
        .Create(Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_CLIENT_ID")
        .WithClientSecret(Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_APP_KEY")
        .WithRedirectUri("https://localhost:8080")
        .Build()

let private authProvider = OnBehalfOfProvider(clientApp)

let private graphServiceClient = GraphServiceClient(authProvider)

let private acquireToken userAssertion scopes = async {
    do! clientApp.AcquireTokenOnBehalfOf(scopes, userAssertion).ExecuteAsync() |> Async.AwaitTask |> Async.Ignore
}

let private retryRequest (request: #IBaseRequest) (send: #IBaseRequest -> Task<_>) = async {
    let retryCount = 5
    return!
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
                    printfn "Warning: Request %s %s failed (#%d/%d). Waiting %O before retrying. %s" request.Method request.RequestUrl i retryCount timeout ex.Message
                    timeout
                ),
                Func<_, _, _, _, _>(fun ex t i ctx -> Task.CompletedTask))
            .ExecuteAsync(fun () -> send request)
        |> Async.AwaitTask
}

let rec private readRemaining initialItems getNextRequest getItems =
    let rec fetchNextItems currentItems allItems = async {
        match getNextRequest currentItems |> Option.ofObj with
        | Some request ->
            let! nextItems = retryRequest request getItems
            return!
                nextItems
                |> Seq.toList
                |> List.append allItems
                |> fetchNextItems nextItems
        | None -> return allItems
    }

    fetchNextItems initialItems (Seq.toList initialItems)

let rec private readAll initialRequest getItems getNextRequest = async {
    let! initialItems = retryRequest initialRequest getItems
    return! readRemaining initialItems getNextRequest getItems
}

module private User =
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

let getGroupsWithPrefix authToken prefix = async {
    do! acquireToken authToken [ "Group.ReadWrite.All" ]
    let! graphGroups =
        readAll
            (graphServiceClient.Groups.Request()
                .Filter(sprintf "startsWith(displayName,'%s')" prefix)
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

let getUsers authToken = async {
    do! acquireToken authToken [ "User.Read.All" ]
    let! users =
        readAll
            (graphServiceClient.Users.Request().Select(User.fields))
            (fun r -> r.GetAsync())
            (fun items -> items.NextPageRequest)
    return users |> List.map User.toDomain
}

// see https://github.com/microsoftgraph/msgraph-sdk-dotnet/issues/528#issuecomment-523083170
type private ExtendedGroup() =
    inherit Group()
        [<Newtonsoft.Json.JsonProperty(DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore, PropertyName = "resourceBehaviorOptions")>]
        member val ResourceBehaviorOptions = [||] with get, set

let private createGroup name = async {
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
    let! group = retryRequest (graphServiceClient.Groups.Request()) (fun request -> request.AddAsync group)

    // `AutoSubscribeNewMembers` must be set separately, see https://docs.microsoft.com/en-us/graph/api/resources/group#properties
    let groupUpdate = Group(AutoSubscribeNewMembers = Nullable true)
    return! retryRequest (graphServiceClient.Groups.[group.Id].Request()) (fun request -> request.UpdateAsync groupUpdate)
}

let private deleteGroup (GroupId groupId) =
    retryRequest
        (graphServiceClient.Groups.[groupId].Request())
        (fun request -> request.DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask)

let private addGroupMember (GroupId groupId) (UserId userId) =
    retryRequest
        (graphServiceClient.Groups.[groupId].Members.References.Request())
        (fun request -> request.AddAsync (User(Id = userId)) |> Async.AwaitTask |> Async.StartAsTask)

let private removeGroupMember (GroupId groupId) (UserId userId) =
    retryRequest
        (graphServiceClient.Groups.[groupId].Members.[userId].Reference.Request())
        (fun request -> request.DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask)

let private applyMemberModifications groupId memberModifications =
    memberModifications
    |> List.map (function
        | AddMember userId -> addGroupMember groupId userId
        | RemoveMember userId -> removeGroupMember groupId userId
    )
    |> Async.Parallel
    |> Async.Ignore

let private changeGroupName (GroupId groupId) newName =
    retryRequest
        (graphServiceClient.Groups.[groupId].Request())
        (fun request -> request.UpdateAsync(Group(Id = groupId, DisplayName = newName, MailNickname = newName)) |> Async.AwaitTask |> Async.StartAsTask) // TODO mail address and aliases are not updated
    |> Async.Ignore

let private applySingleGroupModifications modifications = async {
    match modifications with
    | CreateGroup (name, memberIds) ->
        let! group = createGroup name
        let groupId = GroupId group.Id
        do!
            memberIds
            |> List.map AddMember
            |> applyMemberModifications groupId
    | UpdateGroup (groupId, memberModifications) ->
        do! applyMemberModifications groupId memberModifications
    | ChangeGroupName (groupId, newName) ->
        do! changeGroupName groupId newName
    | DeleteGroup groupId ->
        do! deleteGroup groupId
}

let applyGroupsModifications authToken modifications = async {
    do! acquireToken authToken [ "Group.ReadWrite.All" ]
    do!
        modifications
        |> List.map applySingleGroupModifications
        |> Async.Parallel
        |> Async.Ignore
}

let getUserGroups authToken (UserId userId) = async {
    do! acquireToken authToken [ "Directory.Read.All" ]
    return! readAll
        (graphServiceClient.Users.[userId].MemberOf.Request())
        (fun request -> request.GetAsync())
        (fun items -> items.NextPageRequest)
}

let private getAutoContactIds (UserId userId) = async {
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

let private removeContact (UserId userId) contactId =
    retryRequest
        (graphServiceClient.Users.[userId].Contacts.[contactId].Request())
        (fun request -> request.DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask)

let private removeAutoContacts userId contactIds =
    contactIds
    |> Seq.map (removeContact userId)
    |> Async.Sequential
    |> Async.Ignore

let private addContact (UserId userId) contact =
    retryRequest
        (graphServiceClient.Users.[userId].Contacts.Request())
        (fun request -> request.AddAsync contact)

let private setContactPhoto (UserId userId) contactId (Base64EncodedImage photo) =
    retryRequest
        (graphServiceClient.Users.[userId].Contacts.[contactId].Photo.Content.Request())
        (fun request ->
            async {
                use stream = new MemoryStream(Convert.FromBase64String photo)
                return! request.PutAsync(stream) |> Async.AwaitTask
            }
            |> Async.StartAsTask
        )

let private addAutoContact userId contact = async {
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
        |> addContact userId

    match contact.Photo with
    | Some photo ->
        do! setContactPhoto userId newContact.Id photo |> Async.Ignore
    | None -> ()
}

let private addAutoContacts userId contacts =
    contacts
    |> List.map (addAutoContact userId)
    |> Async.Sequential
    |> Async.Ignore

type private Calendar = {
    Id: string
    Name: string
}

let private getCalendars = async {
    let! calendars =
        retryRequest
            (graphServiceClient.Me.Calendars.Request().Select("id,name"))
            (fun request -> request.GetAsync())
    return
        calendars
        |> Seq.map (fun c -> { Id = c.Id; Name = c.Name })
        |> Seq.toList
}

let private getBirthdayCalendarId = async {
    let! calendars = getCalendars

    return
        calendars
        |> Seq.tryFind (fun c -> CIString c.Name = CIString "Birthdays")
        |> Option.map (fun c -> c.Id)
        |> Option.defaultWith (fun () ->
            let calendarNames =
                calendars
                |> Seq.map (fun c -> c.Name)
                |> String.concat ", "
            failwithf "Birthday calendar not found. Found calendars (%d): %s" (List.length calendars) calendarNames
        )
}

let private getCalendarEventIds calendarId = async {
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

let private updateCalendarEvent calendarId eventId updatedEvent =
    retryRequest
        (graphServiceClient.Me.Calendars.[calendarId].Events.[eventId].Request())
        (fun request -> request.UpdateAsync updatedEvent)

let private getBirthdayCalendarEventCount = async {
    let! birthdayCalendarId = getBirthdayCalendarId
    let! birthdayEventIds = getCalendarEventIds birthdayCalendarId
    return List.length birthdayEventIds
}

let private configureBirthdayReminders = async {
    let! birthdayCalendarId = getBirthdayCalendarId
    let! birthdayEventIds = getCalendarEventIds birthdayCalendarId

    do!
        birthdayEventIds
        |> Seq.map (fun eventId -> async {
            let event' = Event(IsReminderOn = Nullable<_> true, ReminderMinutesBeforeStart = Nullable 0)
            return! updateCalendarEvent birthdayCalendarId eventId event'
        })
        |> Async.Sequential
        |> Async.Ignore
}

let rec private waitUntil workflow = async {
    let! result = workflow
    if result then return ()
    else
        do! Async.Sleep 5000
        return! waitUntil workflow
}

let updateAutoContacts authToken userId contacts = async {
    do! acquireToken authToken [ "Contacts.ReadWrite" ]

    let! birthdayEventCount = getBirthdayCalendarEventCount

    printfn "%O: Removing existing contacts" DateTime.Now
    let! autoContactIds = getAutoContactIds userId
    do! removeAutoContacts userId autoContactIds
    
    printfn "%O: Waiting until birthday calendar is cleared" DateTime.Now
    do! waitUntil (async {
        let! newBirthdayEventCount = getBirthdayCalendarEventCount
        return newBirthdayEventCount = birthdayEventCount - (List.length autoContactIds)
    })
    let! birthdayEventCount = getBirthdayCalendarEventCount

    printfn "%O: Adding new contacts" DateTime.Now
    do! addAutoContacts userId contacts

    printfn "%O: Waiting until birthday calendar is filled" DateTime.Now
    do! waitUntil (async {
        let! newBirthdayEventCount = getBirthdayCalendarEventCount
        return newBirthdayEventCount = birthdayEventCount + (List.length contacts)
    })

    printfn "%O: Configuring birthday events" DateTime.Now
    do! configureBirthdayReminders

    printfn "%O: Finished" DateTime.Now
}
