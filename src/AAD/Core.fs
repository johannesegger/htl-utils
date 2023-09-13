module AAD.Core

open AAD.Configuration
open AAD.Domain
open Microsoft.Graph
open Microsoft.Kiota.Abstractions.Serialization
open System
open System.IO
open System.Threading.Tasks

type Regex = System.Text.RegularExpressions.Regex

let readAll<'a, 'b when 'a: (new: unit -> 'a) and 'a :> IParsable and 'a :> IAdditionalDataHolder> (graphClient: GraphServiceClient) (query: Task<'a>) = async {
    let result = Collections.Generic.List<_>()
    let! firstResponse = query |> Async.AwaitTask
    let iterator =
        PageIterator<'b, 'a>
            .CreatePageIterator(
                graphClient,
                firstResponse,
                (fun item ->
                    result.Add(item)
                    true // continue iteration
                ),
                (fun r -> r)
            )
    do! iterator.IterateAsync() |> Async.AwaitTask
    return result
}

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
        |> readAll<_, Models.Group> graphServiceClient
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
                |> readAll<_, Models.User> graphServiceClient
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
        |> readAll<_, Models.User> graphServiceClient
    return users |> Seq.map User.toDomain |> Seq.toList
}

// see https://github.com/microsoftgraph/msgraph-sdk-dotnet/issues/528#issuecomment-523083170
type ExtendedGroup() =
    inherit Models.Group()
        [<Newtonsoft.Json.JsonProperty(DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore, PropertyName = "resourceBehaviorOptions")>]
        member val ResourceBehaviorOptions = [||] with get, set

let private createGroup (graphServiceClient: GraphServiceClient) name = async {
    let group =
        ExtendedGroup(
            DisplayName = name,
            MailEnabled = Nullable true,
            MailNickname = name,
            SecurityEnabled = Nullable true,
            GroupTypes = Collections.Generic.List([ "Unified" ]),
            Visibility = "Private",
            ResourceBehaviorOptions = [| "SubscribeNewGroupMembers"; "WelcomeEmailDisabled" |]
        )
    let! group = graphServiceClient.Groups.PostAsync(group) |> Async.AwaitTask
    return group

    // // `AutoSubscribeNewMembers` must be set separately, see https://docs.microsoft.com/en-us/graph/api/resources/group#properties
    // let groupUpdate = Models.Group(AutoSubscribeNewMembers = Nullable true)
    // let! group = retryRequest (graphServiceClient.Groups.[group.Id].Request()) (fun request -> request.UpdateAsync groupUpdate)

    // let rec checkGroup i = async {
    //     let! ct = Async.CancellationToken
    //     let! group =
    //         let request = graphServiceClient.Groups.[group.Id].Request().Select("autoSubscribeNewMembers,resourceBehaviorOptions,displayName") :?> BaseRequest
    //         request.SendAsync<ExtendedGroup>(null, ct) |> Async.AwaitTask
    //     if (group.AutoSubscribeNewMembers = Nullable true && group.ResourceBehaviorOptions |> Array.contains "WelcomeEmailDisabled") then
    //         printfn $"Group validation succeeded (%s{group.DisplayName} - %s{group.Id}) (Retry count = %d{i})"
    //         return group
    //     elif i > 1 then
    //         printfn $"Group validation failed (%s{group.DisplayName} - %s{group.Id}) (Retry count = %d{i})"
    //         do! Async.Sleep (TimeSpan.FromSeconds 10.)
    //         return! checkGroup (i - 1)
    //     else return failwith $"Group validation failed (%s{group.DisplayName} - %s{group.Id})"
    // }
    // return! checkGroup (6 * 5)
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
        |> readAll<_, Models.Contact> graphServiceClient
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
            |> List.map (fun v -> Models.EmailAddress(Address = v))
        Models.Contact(
            GivenName = contact.FirstName,
            Surname = contact.LastName,
            DisplayName = contact.DisplayName,
            FileAs = sprintf "%s, %s" contact.LastName contact.FirstName,
            Birthday = birthday,
            HomePhones = Collections.Generic.List(contact.HomePhones),
            MobilePhone = Option.toObj contact.MobilePhone,
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

let private getCalendars (graphServiceClient: GraphServiceClient) = async {
    let! calendars =
        graphServiceClient.Me.Calendars.GetAsync(fun config ->
            config.QueryParameters.Select <- [| "id"; "name" |]
        )
        |> readAll<_, Models.Calendar> graphServiceClient
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
        graphServiceClient.Me.Calendars.[calendarId].Events.GetAsync()
        |> readAll graphServiceClient
    return
        events
        |> Seq.map (fun e -> e.Id)
        |> Seq.toList
}

let private updateCalendarEvent (graphServiceClient: GraphServiceClient) calendarId eventId updatedEvent = async {
    do! graphServiceClient.Me.Calendars.[calendarId].Events.[eventId].PatchAsync(updatedEvent) |> Async.AwaitTask |> Async.Ignore
}

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
            let event' = Models.Event(IsReminderOn = Nullable<_> true, ReminderMinutesBeforeStart = Nullable 0)
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
