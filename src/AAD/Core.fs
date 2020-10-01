module AAD.Core

open AAD.Domain
open Microsoft.Graph
open Microsoft.Graph.Auth
open Microsoft.Identity.Client
open Polly
open System
open System.IO
open System.Security
open System.Threading.Tasks

type Regex = System.Text.RegularExpressions.Regex

type GraphClientAuthentication =
    | OnBehalfOf of UserAssertion
    | UserNamePassword of mailAddress: string * password: SecureString

type GraphClient = {
    Client: GraphServiceClient
    Authentication: GraphClientAuthentication
}

let private configureRequest graphClient (request: #IBaseRequest) =
    match graphClient.Authentication with
    | OnBehalfOf userAssertion -> request.WithUserAssertion(userAssertion)
    | UserNamePassword (userName, password) -> request.WithUsernamePassword(userName, password)

let private retryRequest graphClient (getRequest: GraphServiceClient -> #IBaseRequest) (send: #IBaseRequest -> Task<_>) = async {
    let request = getRequest graphClient.Client |> configureRequest graphClient
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

let rec private readRemaining graphClient initialItems getNextRequest getItems =
    let rec fetchNextItems currentItems allItems = async {
        match getNextRequest currentItems |> Option.ofObj with
        | Some request ->
            let! nextItems = retryRequest graphClient (fun _ -> request) getItems
            return!
                nextItems
                |> Seq.toList
                |> List.append allItems
                |> fetchNextItems nextItems
        | None -> return allItems
    }

    fetchNextItems initialItems (Seq.toList initialItems)

let rec private readAll graphClient getInitialRequest send getNextRequest = async {
    let! initialItems = retryRequest graphClient getInitialRequest send
    return! readRemaining graphClient initialItems getNextRequest send
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

let getGroupsWithPrefix graphClient prefix = async {
    let! graphGroups =
        readAll
            graphClient
            (fun graph ->
                graph.Groups.Request()
                    .Filter(sprintf "startsWith(displayName,'%s')" prefix)
                    .Select("id,displayName,mail"))
            (fun r -> r.GetAsync())
            (fun items -> items.NextPageRequest)
    return!
        graphGroups
        |> Seq.map (fun (g: Microsoft.Graph.Group) -> async {
            let! members =
                readAll
                    graphClient
                    (fun graph -> graph.Groups.[g.Id].Members.Request().Select(User.fields))
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

let getUsers graphClient = async {
    let! users =
        readAll
            graphClient
            (fun graph -> graph.Users.Request().Select(User.fields))
            (fun r -> r.GetAsync())
            (fun items -> items.NextPageRequest)
    return users |> List.map User.toDomain
}

// see https://github.com/microsoftgraph/msgraph-sdk-dotnet/issues/528#issuecomment-523083170
type private ExtendedGroup() =
    inherit Group()
        [<Newtonsoft.Json.JsonProperty(DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore, PropertyName = "resourceBehaviorOptions")>]
        member val ResourceBehaviorOptions = [||] with get, set

let private createGroup graphClient name = async {
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
    let! group = retryRequest graphClient (fun graph -> graph.Groups.Request()) (fun request -> request.AddAsync group)

    // `AutoSubscribeNewMembers` must be set separately, see https://docs.microsoft.com/en-us/graph/api/resources/group#properties
    let groupUpdate = Group(AutoSubscribeNewMembers = Nullable true)
    return! retryRequest graphClient (fun graph -> graph.Groups.[group.Id].Request()) (fun request -> request.UpdateAsync groupUpdate)
}

let private deleteGroup graphClient (GroupId groupId) =
    retryRequest
        graphClient
        (fun graph -> graph.Groups.[groupId].Request())
        (fun request -> request.DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask)

let private addGroupMember graphClient (GroupId groupId) (UserId userId) =
    retryRequest
        graphClient
        (fun graph -> graph.Groups.[groupId].Members.References.Request())
        (fun request -> request.AddAsync (User(Id = userId)) |> Async.AwaitTask |> Async.StartAsTask)

let private removeGroupMember graphClient (GroupId groupId) (UserId userId) =
    retryRequest
        graphClient
        (fun graph -> graph.Groups.[groupId].Members.[userId].Reference.Request())
        (fun request -> request.DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask)

let private applyMemberModifications graphClient groupId memberModifications =
    memberModifications
    |> List.map (function
        | AddMember userId -> addGroupMember graphClient groupId userId
        | RemoveMember userId -> removeGroupMember graphClient groupId userId
    )
    |> Async.Parallel
    |> Async.Ignore

let private changeGroupName graphClient (GroupId groupId) newName = async {
    let! group =
        retryRequest
            graphClient
            (fun graph -> graph.Groups.[groupId].Request())
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
            graphClient
            (fun graph -> graph.Groups.[groupId].Request())
            (fun request -> request.UpdateAsync(update) |> Async.AwaitTask |> Async.StartAsTask)
        |> Async.Ignore
}

let private applySingleGroupModifications graphClient modifications = async {
    match modifications with
    | CreateGroup (name, memberIds) ->
        let! group = createGroup graphClient name
        let groupId = GroupId group.Id
        do!
            memberIds
            |> List.map AddMember
            |> applyMemberModifications graphClient groupId
    | UpdateGroup (groupId, memberModifications) ->
        do! applyMemberModifications graphClient groupId memberModifications
    | ChangeGroupName (groupId, newName) ->
        do! changeGroupName graphClient groupId newName
    | DeleteGroup groupId ->
        do! deleteGroup graphClient groupId
}

let applyGroupsModifications graphClient modifications = async {
    do!
        modifications
        |> List.map (applySingleGroupModifications graphClient)
        |> Async.Parallel
        |> Async.Ignore
}

let getUserGroups graphClient (UserId userId) = async {
    return! readAll
        graphClient
        (fun graph -> graph.Users.[userId].MemberOf.Request())
        (fun request -> request.GetAsync())
        (fun items -> items.NextPageRequest)
}

let private getAutoContactIds graphClient (UserId userId) = async {
    let! contacts =
        readAll
            graphClient
            (fun graph -> graph.Users.[userId].Contacts.Request().Select("id").Filter("categories/any(category: category eq 'htl-utils-auto-generated')"))
            (fun r -> r.GetAsync())
            (fun items -> items.NextPageRequest)
    return
        contacts
        |> Seq.map (fun c -> c.Id)
        |> Seq.toList
}

let private removeContact graphClient (UserId userId) contactId =
    retryRequest
        graphClient
        (fun graph -> graph.Users.[userId].Contacts.[contactId].Request())
        (fun request -> request.DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask)

let private removeAutoContacts graphClient userId contactIds =
    contactIds
    |> Seq.map (removeContact graphClient userId)
    |> Async.Sequential
    |> Async.Ignore

let private addContact graphClient (UserId userId) contact =
    retryRequest
        graphClient
        (fun graph -> graph.Users.[userId].Contacts.Request())
        (fun request -> request.AddAsync contact)

let private setContactPhoto graphClient (UserId userId) contactId (Base64EncodedImage photo) =
    retryRequest
        graphClient
        (fun graph -> graph.Users.[userId].Contacts.[contactId].Photo.Content.Request())
        (fun request ->
            async {
                use stream = new MemoryStream(Convert.FromBase64String photo)
                return! request.PutAsync(stream) |> Async.AwaitTask
            }
            |> Async.StartAsTask
        )

let private addAutoContact graphClient userId contact = async {
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
        |> addContact graphClient userId

    match contact.Photo with
    | Some photo ->
        do! setContactPhoto graphClient userId newContact.Id photo |> Async.Ignore
    | None -> ()
}

let private addAutoContacts graphClient userId contacts =
    contacts
    |> List.map (addAutoContact graphClient userId)
    |> Async.Sequential
    |> Async.Ignore

type private Calendar = {
    Id: string
    Name: string
}

let private getCalendars graphClient = async {
    let! calendars =
        retryRequest
            graphClient
            (fun graph -> graph.Me.Calendars.Request().Select("id,name"))
            (fun request -> request.GetAsync())
    return
        calendars
        |> Seq.map (fun c -> { Id = c.Id; Name = c.Name })
        |> Seq.toList
}

let private getBirthdayCalendarId graphClient = async {
    let! calendars = getCalendars graphClient

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

let private getCalendarEventIds graphClient calendarId = async {
    let! events =
        readAll
            graphClient
            (fun graph -> graph.Me.Calendars.[calendarId].Events.Request())
            (fun r -> r.GetAsync())
            (fun items -> items.NextPageRequest)
    return
        events
        |> Seq.map (fun e -> e.Id)
        |> Seq.toList
}

let private updateCalendarEvent graphClient calendarId eventId updatedEvent =
    retryRequest
        graphClient
        (fun graph -> graph.Me.Calendars.[calendarId].Events.[eventId].Request())
        (fun request -> request.UpdateAsync updatedEvent)

let private getBirthdayCalendarEventCount graphClient = async {
    let! birthdayCalendarId = getBirthdayCalendarId graphClient
    let! birthdayEventIds = getCalendarEventIds graphClient birthdayCalendarId
    return List.length birthdayEventIds
}

let private configureBirthdayReminders graphClient = async {
    let! birthdayCalendarId = getBirthdayCalendarId graphClient
    let! birthdayEventIds = getCalendarEventIds graphClient birthdayCalendarId

    do!
        birthdayEventIds
        |> Seq.map (fun eventId -> async {
            let event' = Event(IsReminderOn = Nullable<_> true, ReminderMinutesBeforeStart = Nullable 0)
            return! updateCalendarEvent graphClient birthdayCalendarId eventId event'
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

let updateAutoContacts graphClient userId contacts = async {
    let! birthdayEventCount = getBirthdayCalendarEventCount graphClient

    printfn "%O: Removing existing contacts" DateTime.Now
    let! autoContactIds = getAutoContactIds graphClient userId
    do! removeAutoContacts graphClient userId autoContactIds

    printfn "%O: Waiting until birthday calendar is cleared" DateTime.Now
    do! waitUntil (async {
        let! newBirthdayEventCount = getBirthdayCalendarEventCount graphClient
        return newBirthdayEventCount = birthdayEventCount - (List.length autoContactIds)
    })
    let! birthdayEventCount = getBirthdayCalendarEventCount graphClient

    printfn "%O: Adding new contacts" DateTime.Now
    do! addAutoContacts graphClient userId contacts

    printfn "%O: Waiting until birthday calendar is filled" DateTime.Now
    do! waitUntil (async {
        let! newBirthdayEventCount = getBirthdayCalendarEventCount graphClient
        return newBirthdayEventCount = birthdayEventCount + (List.length contacts)
    })

    printfn "%O: Configuring birthday events" DateTime.Now
    do! configureBirthdayReminders graphClient

    printfn "%O: Finished" DateTime.Now
}
