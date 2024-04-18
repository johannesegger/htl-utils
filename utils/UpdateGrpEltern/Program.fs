open Azure.Identity
open Microsoft.Graph.Beta
open Sokrates
open System
open System.Text.RegularExpressions
open System.Threading.Tasks

let dryRun = true

module List =
    let diff (oldItems, oldItemToKey) (newItems, newItemToKey) =
        let oldItemsSet = oldItems |> List.map oldItemToKey |> Set.ofList
        let newItemsSet = newItems |> List.map newItemToKey |> Set.ofList
        let removed =
            oldItems
            |> List.filter (fun v -> not <| Set.contains (oldItemToKey v) newItemsSet)
        let added =
            newItems
            |> List.filter (fun v -> not <| Set.contains (newItemToKey v) oldItemsSet)
        (added, removed)

let sokratesApi = SokratesApi.FromEnvironment()
let allStudents = sokratesApi.FetchStudents None None |> Async.RunSynchronously
let students =
    allStudents
    |> List.filter (fun v -> not <| Regex.IsMatch(v.SchoolClass, @"^\d+(AVMB|ABMB)$"))
let studentContacts = sokratesApi.FetchStudentContactInfos (students |> List.map _.Id) None |> Async.RunSynchronously
let studentContactsById =
    studentContacts
    |> List.map (fun v ->
        let contacts =
            v.ContactAddresses
            |> List.choose _.EMailAddress
            |> List.filter (fun v -> v.Contains("@"))
        v.StudentId, contacts
    )
    |> Map.ofList
let studentsWithAddresses =
    students
    |> List.map (fun student ->
        let mailAddresses = Map.tryFind student.Id studentContactsById |> Option.defaultValue []
        student, mailAddresses
    )
    |> List.groupBy (fst >> _.SchoolClass)
    |> List.map (fun (schoolClass, students) ->
        {|
            GroupName = $"GrpEltern%s{schoolClass}"
            StudentsWithoutAddresses =
                students
                |> List.filter (snd >> List.isEmpty)
                |> List.sortBy (fst >> fun v -> v.LastName, v.FirstName1)
                |> List.map (fst >> fun v -> $"%s{v.LastName} %s{v.FirstName1}")
            StudentAddresses =
                students
                |> List.collect snd
                |> List.distinct
        |}
    )

let graphClient =
    let config = AAD.Configuration.Config.fromEnvironment ()
    let scopes = [| "GroupMember.Read.All" |]
    let deviceCodeCredential =
        DeviceCodeCredentialOptions (
            ClientId = config.OidcConfig.AppId,
            TenantId = config.OidcConfig.TenantId,
            DeviceCodeCallback = (fun code ct ->
                Console.ForegroundColor <- ConsoleColor.Yellow
                printfn "%s" code.Message
                Console.ResetColor()
                Task.CompletedTask
            )
        )
        |> DeviceCodeCredential
    new GraphServiceClient(deviceCodeCredential, scopes)

let formatGraphErrors errorTitle wf = async {
    try
        return! wf
    with
        | :? AggregateException as e ->
            match e.InnerException with
            | :? Models.ODataErrors.ODataError as e ->
                return failwith $"%s{errorTitle}: %s{e.Error.Message}"
            | _ -> return raise e.InnerException
        | e -> return raise e
}

let getDirectoryObjectReference (object: #Models.DirectoryObject) =
    Models.ReferenceCreate(OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{object.Id}")

let retryUntilTrue wf = async {
    let mutable isDone = false
    while not isDone do
        let! result = wf
        isDone <- result
}

type UserType = MemberUser | GuestUser
type GroupMember = {
    UserId: string
    MailAddress: string
    UserType: UserType
}
type ExistingGroup = {
    GroupId: string
    Name: string
    Members: GroupMember list
}

let parseGroupMember (user: Models.User) =
    let userType =
        if user.UserType = "Member" then MemberUser
        elif user.UserType = "Guest" then GuestUser
        else failwith $"User %s{user.DisplayName} has unknown user type: %s{user.UserType}"
    { UserId = user.Id; MailAddress = user.Mail; UserType = userType }

let aadParentGroups =
    graphClient.Groups.GetAsync(fun v -> v.QueryParameters.Filter <- "startsWith(displayName, 'GrpEltern')")
    |> AAD.Core.readAll<_, Models.Group> graphClient
    |> Async.RunSynchronously
// aadParentGroups
// |> Seq.map _.DisplayName
// |> Seq.iter (printfn "%s")
let aadParentGroupsWithMembers =
    aadParentGroups
    |> Seq.filter (fun v -> v.DisplayName <> "GrpEltern")
    |> Seq.map (fun group -> async {
        let! groupMembers =
            graphClient.Groups.[group.Id].Members.GraphUser.GetAsync()
            |> AAD.Core.readAll<_, Models.User> graphClient
        let members = groupMembers |> Seq.map parseGroupMember |> Seq.toList
        return { GroupId = group.Id; Name = group.DisplayName; Members = members }
    })
    |> Async.Parallel
    |> Async.RunSynchronously
    |> List.ofArray
// aadParentGroupsWithMembers
// |> Seq.iter (fun group ->
//     printfn $"%s{group.Name}:"
//     group.Members
//     |> Seq.map (fun v -> $"%s{v.MailAddress} - %A{v.UserType}")
//     |> Seq.iter (printfn "  * %s")
// )

let existingParents =
    aadParentGroupsWithMembers
    |> List.collect _.Members
let (parentsToAdd, parentsToRemove) =
    let existingGuests = existingParents |> List.filter (fun v -> v.UserType = GuestUser)
    let newGuests =
        studentsWithAddresses
        |> List.collect _.StudentAddresses
        |> List.filter (fun v -> not <| v.EndsWith("@htlvb.at"))
        |> List.map (fun v -> v.ToLowerInvariant())
        |> List.distinct
    List.diff
        (existingGuests, _.MailAddress.ToLowerInvariant())
        (newGuests, id)

printfn $"Removing %d{parentsToRemove.Length}/%d{existingParents.Length} parents"
parentsToRemove
|> List.map (fun v -> async {
    printfn $"Removing %s{v.MailAddress}"
    if not dryRun then
        do! graphClient.Users.[v.UserId].DeleteAsync() |> Async.AwaitTask |> formatGraphErrors $"Error while removing %s{v.MailAddress}"
})
|> Async.Parallel
|> Async.Ignore
|> Async.RunSynchronously

printfn $"Adding %d{parentsToAdd.Length} parent users"
parentsToAdd
|> List.map (fun v -> async {
    printfn $"Adding %s{v}"
    let invitation =
        Models.Invitation(
            InvitedUserEmailAddress = v,
            InviteRedirectUrl = "https://htlvb.at",
            InvitedUserType = "Guest",
            SendInvitationMessage = false
        )
    if not dryRun then
        do! graphClient.Invitations.PostAsync(invitation) |> Async.AwaitTask |> formatGraphErrors $"Error while adding %s{v}" |> Async.Ignore
})
|> Async.Parallel
|> Async.Ignore
|> Async.RunSynchronously

let (groupsToAdd, groupsToRemove) =
    List.diff (aadParentGroupsWithMembers, _.Name) (studentsWithAddresses, _.GroupName)

printfn $"Removing %d{groupsToRemove.Length}/%d{aadParentGroupsWithMembers.Length} parent groups"
groupsToRemove
|> List.map (fun v -> async {
    printfn $"Removing %s{v.Name}"
    if not dryRun then
        do! graphClient.Groups.[v.GroupId].DeleteAsync() |> Async.AwaitTask |> formatGraphErrors $"Error while removing %s{v.Name}"
})
|> Async.Parallel
|> Async.Ignore
|> Async.RunSynchronously

printfn $"Adding %d{groupsToAdd.Length} parent groups"
let teachersGroup =
    graphClient.Groups.GetAsync(fun v -> v.QueryParameters.Filter <- "displayName eq 'GrpLehrer'")
    |> AAD.Core.readAll<_, Models.Group> graphClient
    |> Async.RunSynchronously
    |> Seq.exactlyOne
let adminUser =
    graphClient.Users.GetAsync(fun v -> v.QueryParameters.Filter <- "userPrincipalName eq 'admin@htlvb.at'")
    |> AAD.Core.readAll<_, Models.User> graphClient
    |> Async.RunSynchronously
    |> Seq.exactlyOne
// groupsToAdd
[ {| GroupName = "GrpElternEggjTest" |} ]
|> List.map (fun v -> async {
    printfn $"Adding %s{v.GroupName}"
    let group = Models.Group(
        DisplayName = v.GroupName,
        GroupTypes = Collections.Generic.List<_>([ "Unified" ]),
        MailEnabled = true,
        MailNickname = v.GroupName,
        ResourceBehaviorOptions = Collections.Generic.List [ "SubscribeNewGroupMembers"; "WelcomeEmailDisabled" ],
        SecurityEnabled = false,
        Visibility = "HiddenMembership"
    )
    let patch = Models.Group(
        // AcceptedSenders = Collections.Generic.List<_>([ teachersGroup :> Models.DirectoryObject ]),
        AccessType = Models.GroupAccessType.Private
    )
    if true || not dryRun then
        let! aadGroup = graphClient.Groups.PostAsync(group) |> Async.AwaitTask |> formatGraphErrors $"Error while adding %s{v.GroupName}"
        // let! aadGroup = graphClient.Groups.["b958388f-0b84-42e8-abad-fd59d58aeefe"].GetAsync() |> Async.AwaitTask
        do! retryUntilTrue (async {
            try
                let! g = graphClient.Groups.[aadGroup.Id].GetAsync() |> Async.AwaitTask
                printfn "%A %b" g (isNull g)
                return true
            with e -> return false
        })
        do! graphClient.Groups.[aadGroup.Id].PatchAsync(patch) |> Async.AwaitTask |> formatGraphErrors $"Error while patching %s{v.GroupName}" |> Async.Ignore
        do! graphClient.Groups.[aadGroup.Id].Owners.[aadGroup.Owners.[0].Id].Ref.DeleteAsync() |> Async.AwaitTask |> formatGraphErrors "Error while removing default owner" |> Async.Ignore
        do! graphClient.Groups.[aadGroup.Id].Owners.Ref.PostAsync(getDirectoryObjectReference adminUser) |> Async.AwaitTask |> formatGraphErrors "Error while adding group owner"
        do! graphClient.Groups.[aadGroup.Id].AcceptedSenders.Ref.PostAsync(getDirectoryObjectReference teachersGroup) |> Async.AwaitTask |> formatGraphErrors "Error while adding accepted senders" |> Async.Ignore
})
|> Async.Parallel
|> Async.Ignore
|> Async.RunSynchronously
