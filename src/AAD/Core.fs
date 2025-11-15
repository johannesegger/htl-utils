module AAD.Core

open AAD.Configuration
open AAD.Domain
open Microsoft.Graph.Beta
open System
open System.IO
open System.Text.RegularExpressions

module private User =
    let internal fields = [| "id"; "userPrincipalName"; "givenName"; "surname"; "proxyAddresses" |]
    let toDomain (user: Models.User) =
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
        graphServiceClient.Groups.GetAsync(fun config ->
            config.QueryParameters.Top <- 999
            config.QueryParameters.Filter <- $"startsWith(displayName,'%s{prefix}')"
            config.QueryParameters.Select <- [| "id"; "displayName"; "mail" |]
        )
        |> graphServiceClient.ReadAll<_, Models.Group>
        |> GraphServiceClient.formatError "Error while getting groups"
    return!
        graphGroups
        |> Seq.filter (fun (g: Models.Group) ->
            match excludePattern with
            | Some v -> not <| v.IsMatch(g.DisplayName)
            | None -> true
        )
        |> Seq.map (fun (g: Models.Group) -> async {
            let! members =
                graphServiceClient.Groups[g.Id].Members.GraphUser.GetAsync(fun config ->
                    config.QueryParameters.Select <- User.fields
                )
                |> graphServiceClient.ReadAll<_, Models.User>
                |> GraphServiceClient.formatError "Error while getting group members"
            return
                {
                    Id = GroupId g.Id
                    Name = g.DisplayName
                    Mail = g.Mail
                    Members =
                        members
                        |> Seq.map (fun m -> UserId m.Id)
                        |> Seq.toList
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
        graphServiceClient.Users.GetAsync(fun config ->
            config.QueryParameters.Select <- User.fields
        )
        |> graphServiceClient.ReadAll<_, Models.User>
        |> GraphServiceClient.formatError "Error while getting users"
    return users |> Seq.map User.toDomain |> Seq.toList
}

let private createGroup (graphServiceClient: GraphServiceClient) name = async {
    let group =
        Models.Group(
            DisplayName = name,
            MailEnabled = Nullable true,
            MailNickname = name,
            SecurityEnabled = Nullable true,
            GroupTypes = Collections.Generic.List([ "Unified" ]),
            Visibility = "Private",
            ResourceBehaviorOptions = Collections.Generic.List [ "SubscribeNewGroupMembers"; "WelcomeEmailDisabled" ]
        )
    return! graphServiceClient.Groups.PostAsync(group) |> Async.AwaitTask
}

let private deleteGroup (graphServiceClient: GraphServiceClient) (GroupId groupId) = async {
    do! graphServiceClient.Groups.[groupId].DeleteAsync() |> Async.AwaitTask
}

let private addGroupMember (graphServiceClient: GraphServiceClient) (GroupId groupId) (UserId userId) = async {
    do! graphServiceClient.Groups.[groupId].Members.Ref.PostAsync(Models.ReferenceCreate(OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/%s{userId}")) |> Async.AwaitTask
}

let private removeGroupMember (graphServiceClient: GraphServiceClient) (GroupId groupId) (UserId userId) = async {
    do! graphServiceClient.Groups.[groupId].Members.[userId].Ref.DeleteAsync() |> Async.AwaitTask
}

let private applyMemberModifications (graphServiceClient: GraphServiceClient) groupId memberModifications =
    memberModifications
    |> List.map (function
        | AddMember userId -> addGroupMember graphServiceClient groupId userId
        | RemoveMember userId -> removeGroupMember graphServiceClient groupId userId
    )
    |> Async.Parallel
    |> Async.Ignore

let private changeGroupName (graphServiceClient: GraphServiceClient) (GroupId groupId) newName = async {
    // let! group = graphServiceClient.Groups.[groupId].GetAsync() |> Async.AwaitTask
    let update =
        Models.Group(
            DisplayName = newName,
            MailNickname = newName
            // Mail = group.Mail.Replace(group.MailNickname, newName), // TODO mail address is read-only and can't be changed
            // ProxyAddresses = (group.ProxyAddresses |> Seq.map (fun address -> Regex.Replace(address, "(?<=^(SMTP|smtp):)[^@]*", newName))) // TODO insufficient permissions for updating proxy addresses
        )
    do! graphServiceClient.Groups.[groupId].PatchAsync(update) |> Async.AwaitTask |> Async.Ignore
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
        graphServiceClient.Users.[userId].Contacts.GetAsync(fun config ->
            config.QueryParameters.Select <- [| "id" |]
            config.QueryParameters.Filter <- "categories/any(category: category eq 'htl-utils-auto-generated')"
        )
        |> graphServiceClient.ReadAll<_, Models.Contact>
        |> GraphServiceClient.formatError "Error while getting user contacts"
    return
        contacts
        |> Seq.map (fun c -> c.Id)
        |> Seq.toList
}

let private removeContact (graphServiceClient: GraphServiceClient) (UserId userId) contactId = async {
    do! graphServiceClient.Users.[userId].Contacts.[contactId].DeleteAsync() |> Async.AwaitTask
}

let private removeAutoContacts (graphServiceClient: GraphServiceClient) userId contactIds =
    contactIds
    |> Seq.map (removeContact graphServiceClient userId)
    |> Async.Sequential
    |> Async.Ignore

let private addContact (graphServiceClient: GraphServiceClient) (UserId userId) contact = async {
    return! graphServiceClient.Users.[userId].Contacts.PostAsync(contact) |> Async.AwaitTask
}

let private setContactPhoto (graphServiceClient: GraphServiceClient) (UserId userId) contactId (Base64EncodedImage photo) = async {
    use stream = new MemoryStream(Convert.FromBase64String photo)
    do! graphServiceClient.Users.[userId].Contacts.[contactId].Photo.Content.PutAsync(stream) |> Async.AwaitTask |> Async.Ignore
}

let private addAutoContact (graphServiceClient: GraphServiceClient) userId contact = async {
    let! newContact =
        let birthday =
            contact.Birthday
            |> Option.map (fun date -> DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero))
            |> Option.toNullable
        let mailAddresses =
            contact.MailAddresses
            |> List.map (fun v -> Models.TypedEmailAddress(Address = v, Type = Models.EmailType.Work))
        Models.Contact(
            GivenName = contact.FirstName,
            Surname = contact.LastName,
            DisplayName = contact.DisplayName,
            FileAs = sprintf "%s, %s" contact.LastName contact.FirstName,
            Birthday = birthday,
            Phones =
                Collections.Generic.List([
                    yield! contact.HomePhones |> List.map (fun v -> Models.Phone(Number = v, Type = Models.PhoneType.Home))
                    yield! contact.MobilePhone |> Option.map (fun v -> Models.Phone(Number = v, Type = Models.PhoneType.Mobile)) |> Option.toList
                ]),
            EmailAddresses = Collections.Generic.List(mailAddresses),
            Categories = Collections.Generic.List([ "htl-utils-auto-generated" ])
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

let private getCalendars (graphServiceClient: GraphServiceClient) (UserId userId) = async {
    let! calendars =
        graphServiceClient.Users.[userId].Calendars.GetAsync(fun config ->
            config.QueryParameters.Select <- [| "id"; "name" |]
        )
        |> graphServiceClient.ReadAll<_, Models.Calendar>
        |> GraphServiceClient.formatError "Error while getting calendars"
    return
        calendars
        |> Seq.map (fun c -> { Id = c.Id; Name = c.Name })
        |> Seq.toList
}

let private getBirthdayCalendarId (graphServiceClient: GraphServiceClient) userId = async {
    let! calendars = getCalendars graphServiceClient userId

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

let private getCalendarEventIds (graphServiceClient: GraphServiceClient) (UserId userId) calendarId = async {
    let! events =
        graphServiceClient.Users.[userId].Calendars.[calendarId].Events.GetAsync()
        |> graphServiceClient.ReadAll<_, Models.Event>
        |> GraphServiceClient.formatError "Error while getting calendar events"
    return
        events
        |> Seq.map (fun e -> e.Id)
        |> Seq.toList
}

let private updateCalendarEvent (graphServiceClient: GraphServiceClient) (UserId userId) eventId updatedEvent = async {
    do! graphServiceClient.Users.[userId].Events.[eventId].PatchAsync(updatedEvent) |> Async.AwaitTask |> Async.Ignore
}

let private getBirthdayCalendarEventCount (graphServiceClient: GraphServiceClient) userId = async {
    let! birthdayCalendarId = getBirthdayCalendarId graphServiceClient userId
    let! birthdayEventIds = getCalendarEventIds graphServiceClient userId birthdayCalendarId
    return List.length birthdayEventIds
}

let private configureBirthdayReminders (graphServiceClient: GraphServiceClient) userId = async {
    let! birthdayCalendarId = getBirthdayCalendarId graphServiceClient userId
    let! birthdayEventIds = getCalendarEventIds graphServiceClient userId birthdayCalendarId

    do!
        birthdayEventIds
        |> Seq.map (fun eventId -> async {
            let event' = Models.Event(IsReminderOn = Nullable<_> true, ReminderMinutesBeforeStart = Nullable 0)
            return! updateCalendarEvent graphServiceClient userId eventId event'
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
    let! birthdayEventCount = getBirthdayCalendarEventCount graphServiceClient userId

    printfn "%O: Removing existing contacts" DateTime.Now
    let! autoContactIds = getAutoContactIds graphServiceClient userId
    do! removeAutoContacts graphServiceClient userId autoContactIds

    printfn "%O: Waiting until birthday calendar is cleared" DateTime.Now
    do! waitUntil (async {
        let! newBirthdayEventCount = getBirthdayCalendarEventCount graphServiceClient userId
        return newBirthdayEventCount = birthdayEventCount - (List.length autoContactIds)
    })
    let! birthdayEventCount = getBirthdayCalendarEventCount graphServiceClient userId

    printfn "%O: Adding new contacts" DateTime.Now
    do! addAutoContacts graphServiceClient userId contacts

    printfn "%O: Waiting until birthday calendar is filled" DateTime.Now
    do! waitUntil (async {
        let! newBirthdayEventCount = getBirthdayCalendarEventCount graphServiceClient userId
        return newBirthdayEventCount = birthdayEventCount + (List.length contacts)
    })

    printfn "%O: Configuring birthday events" DateTime.Now
    do! configureBirthdayReminders graphServiceClient userId

    printfn "%O: Finished" DateTime.Now
}
