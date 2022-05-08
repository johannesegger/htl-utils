module AAD.Core

open AAD.Configuration
open AAD.Domain
open Microsoft.Graph
open Polly
open System
open System.IO
open System.Security
open System.Threading.Tasks

type Regex = System.Text.RegularExpressions.Regex

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

let rec private readAll initialRequest send getNextRequest = async {
    let! initialItems = retryRequest initialRequest send
    return! readRemaining initialItems getNextRequest send
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
            UserName = String.trySplitAt "@" user.UserPrincipalName |> Option.map fst |> Option.defaultValue user.UserPrincipalName
            FirstName = ifNullEmpty user.GivenName
            LastName = ifNullEmpty user.Surname
            MailAddresses =
                user.ProxyAddresses
                |> Seq.sortBy toSortable
                |> Seq.choose tryGetMailAddressFromProxyAddress
                |> Seq.toList
        }

let private getFilteredGroups (graphServiceClient: GraphServiceClient) prefix (excludePattern: Regex option) = async {
    let! graphGroups =
        readAll
            (graphServiceClient.Groups.Request()
                .Filter(sprintf "startsWith(displayName,'%s')" prefix)
                .Select("id,displayName,mail"))
            (fun r -> r.GetAsync())
            (fun items -> items.NextPageRequest)
    return!
        graphGroups
        |> Seq.filter (fun (g: Microsoft.Graph.Group) ->
            match excludePattern with
            | Some v -> not <| v.IsMatch(g.DisplayName)
            | None -> true
        )
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

let getPredefinedGroups (graphServiceClient: GraphServiceClient) = reader {
    let! config = Reader.environment
    return getFilteredGroups graphServiceClient config.PredefinedGroupPrefix config.ManuallyManagedGroupsPattern
}

let getUsers (graphServiceClient: GraphServiceClient) = async {
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

let private createGroup (graphServiceClient: GraphServiceClient) name = async {
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

let private deleteGroup (graphServiceClient: GraphServiceClient) (GroupId groupId) =
    retryRequest
        (graphServiceClient.Groups.[groupId].Request())
        (fun request -> request.DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask)

let private addGroupMember (graphServiceClient: GraphServiceClient) (GroupId groupId) (UserId userId) =
    retryRequest
        (graphServiceClient.Groups.[groupId].Members.References.Request())
        (fun request -> request.AddAsync (User(Id = userId)) |> Async.AwaitTask |> Async.StartAsTask)

let private removeGroupMember (graphServiceClient: GraphServiceClient) (GroupId groupId) (UserId userId) =
    retryRequest
        (graphServiceClient.Groups.[groupId].Members.[userId].Reference.Request())
        (fun request -> request.DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask)

let private applyMemberModifications (graphServiceClient: GraphServiceClient) groupId memberModifications =
    memberModifications
    |> List.map (function
        | AddMember userId -> addGroupMember graphServiceClient groupId userId
        | RemoveMember userId -> removeGroupMember graphServiceClient groupId userId
    )
    |> Async.Parallel
    |> Async.Ignore

let private changeGroupName (graphServiceClient: GraphServiceClient) (GroupId groupId) newName = async {
    let! group =
        retryRequest
            (graphServiceClient.Groups.[groupId].Request())
            (fun request -> request.GetAsync())
    let update =
        Group(
            DisplayName = newName,
            MailNickname = newName
            // Mail = group.Mail.Replace(group.MailNickname, newName), // TODO mail address is read-only and can't be changed
            // ProxyAddresses = (group.ProxyAddresses |> Seq.map (fun address -> Regex.Replace(address, "(?<=^(SMTP|smtp):)[^@]*", newName))) // TODO insufficient permissions for updating proxy addresses
        )
    do!
        retryRequest
            (graphServiceClient.Groups.[groupId].Request())
            (fun request -> request.UpdateAsync(update) |> Async.AwaitTask |> Async.StartAsTask)
        |> Async.Ignore
}

let private applySingleGroupModifications (graphServiceClient: GraphServiceClient) modifications = async {
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
    | ChangeGroupName (groupId, newName) ->
        do! changeGroupName graphServiceClient groupId newName
    | DeleteGroup groupId ->
        do! deleteGroup graphServiceClient groupId
}

let applyGroupsModifications (graphServiceClient: GraphServiceClient) modifications = async {
    do!
        modifications
        |> List.map (applySingleGroupModifications graphServiceClient)
        |> Async.Parallel
        |> Async.Ignore
}

let private getAutoContactIds (graphServiceClient: GraphServiceClient) (UserId userId) = async {
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

let private removeContact (graphServiceClient: GraphServiceClient) (UserId userId) contactId =
    retryRequest
        (graphServiceClient.Users.[userId].Contacts.[contactId].Request())
        (fun request -> request.DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask)

let private removeAutoContacts (graphServiceClient: GraphServiceClient) userId contactIds =
    contactIds
    |> Seq.map (removeContact graphServiceClient userId)
    |> Async.Sequential
    |> Async.Ignore

let private addContact (graphServiceClient: GraphServiceClient) (UserId userId) contact =
    retryRequest
        (graphServiceClient.Users.[userId].Contacts.Request())
        (fun request -> request.AddAsync contact)

let private setContactPhoto (graphServiceClient: GraphServiceClient) (UserId userId) contactId (Base64EncodedImage photo) =
    retryRequest
        (graphServiceClient.Users.[userId].Contacts.[contactId].Photo.Content.Request())
        (fun request ->
            async {
                use stream = new MemoryStream(Convert.FromBase64String photo)
                return! request.PutAsync(stream) |> Async.AwaitTask
            }
            |> Async.StartAsTask
        )

let private addAutoContact (graphServiceClient: GraphServiceClient) userId contact = async {
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
    | Some photo ->
        do! setContactPhoto graphServiceClient userId newContact.Id photo |> Async.Ignore
    | None -> ()
}

let private addAutoContacts (graphServiceClient: GraphServiceClient) userId contacts =
    contacts
    |> List.map (addAutoContact graphServiceClient userId)
    |> Async.Sequential
    |> Async.Ignore

type private Calendar = {
    Id: string
    Name: string
}

let private getCalendars (graphServiceClient: GraphServiceClient) = async {
    let! calendars =
        retryRequest
            (graphServiceClient.Me.Calendars.Request().Select("id,name"))
            (fun request -> request.GetAsync())
    return
        calendars
        |> Seq.map (fun c -> { Id = c.Id; Name = c.Name })
        |> Seq.toList
}

let private getBirthdayCalendarId (graphServiceClient: GraphServiceClient) = async {
    let! calendars = getCalendars graphServiceClient

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

let private getCalendarEventIds (graphServiceClient: GraphServiceClient) calendarId = async {
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

let private updateCalendarEvent (graphServiceClient: GraphServiceClient) calendarId eventId updatedEvent =
    retryRequest
        (graphServiceClient.Me.Calendars.[calendarId].Events.[eventId].Request())
        (fun request -> request.UpdateAsync updatedEvent)

let private getBirthdayCalendarEventCount (graphServiceClient: GraphServiceClient) = async {
    let! birthdayCalendarId = getBirthdayCalendarId graphServiceClient
    let! birthdayEventIds = getCalendarEventIds graphServiceClient birthdayCalendarId
    return List.length birthdayEventIds
}

let private configureBirthdayReminders (graphServiceClient: GraphServiceClient) = async {
    let! birthdayCalendarId = getBirthdayCalendarId graphServiceClient
    let! birthdayEventIds = getCalendarEventIds graphServiceClient birthdayCalendarId

    do!
        birthdayEventIds
        |> Seq.map (fun eventId -> async {
            let event' = Event(IsReminderOn = Nullable<_> true, ReminderMinutesBeforeStart = Nullable 0)
            return! updateCalendarEvent graphServiceClient birthdayCalendarId eventId event'
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

let updateAutoContacts (graphServiceClient: GraphServiceClient) userId contacts = async {
    let! birthdayEventCount = getBirthdayCalendarEventCount graphServiceClient

    printfn "%O: Removing existing contacts" DateTime.Now
    let! autoContactIds = getAutoContactIds graphServiceClient userId
    do! removeAutoContacts graphServiceClient userId autoContactIds

    printfn "%O: Waiting until birthday calendar is cleared" DateTime.Now
    do! waitUntil (async {
        let! newBirthdayEventCount = getBirthdayCalendarEventCount graphServiceClient
        return newBirthdayEventCount = birthdayEventCount - (List.length autoContactIds)
    })
    let! birthdayEventCount = getBirthdayCalendarEventCount graphServiceClient

    printfn "%O: Adding new contacts" DateTime.Now
    do! addAutoContacts graphServiceClient userId contacts

    printfn "%O: Waiting until birthday calendar is filled" DateTime.Now
    do! waitUntil (async {
        let! newBirthdayEventCount = getBirthdayCalendarEventCount graphServiceClient
        return newBirthdayEventCount = birthdayEventCount + (List.length contacts)
    })

    printfn "%O: Configuring birthday events" DateTime.Now
    do! configureBirthdayReminders graphServiceClient

    printfn "%O: Finished" DateTime.Now
}
