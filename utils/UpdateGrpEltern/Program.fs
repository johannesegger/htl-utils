open AAD
open Domain
open Microsoft.Graph.Beta
open Sokrates

let debug = false
let dryRun = false

async {
    let sokratesApi = SokratesApi.FromEnvironment()
    let! sokratesParentAddresses = sokratesApi.GetParentGroups()

    let sokratesParentAddresses = [
        {
            GroupName = "GrpElternEggjTest"
            StudentsWithoutAddresses = []
            StudentAddresses = ["j.egger@posteo.at"]
        }
    ]

    if debug then
        printfn "== Target state =="
        sokratesParentAddresses
        |> Seq.iter (fun group ->
            printfn $"* %s{group.GroupName}:"
            printfn $"  * %d{group.StudentAddresses.Length} addresses:"
            group.StudentAddresses
            |> Seq.iter (printfn "    * %s")
            printfn $"  * %d{group.StudentsWithoutAddresses.Length} students without parent addresses:"
            group.StudentsWithoutAddresses
            |> Seq.iter (printfn "    * %s")
        )

    let aadConfig = AAD.Configuration.Config.fromEnvironment ()
    let graphClient =
        // GraphServiceClientFactory.createWithAppSecret aadConfig.OidcConfig
        GraphServiceClientFactory.createWithDeviceCode aadConfig.OidcConfig [| "Directory.ReadWrite.All" |] "HtlUtils.UpdateGrpEltern"

    let! (existingParentGroups: ExistingParentGroup list) = graphClient.GetParentGroups()
    let existingParentGroups: ExistingParentGroup list = []
    if debug then
        printfn "== Current state =="
        existingParentGroups
        |> Seq.iter (fun group ->
            printfn $"* %s{group.Name}:"
            group.Members
            |> Seq.iter (fun v -> printfn $"  * %s{v.MailAddress} (%A{v.UserType})")
        )

    let (parentsToAdd, parentsToRemove) = getParentsDiff sokratesParentAddresses existingParentGroups

    printfn $"== Removing %d{parentsToRemove.Length}/%d{existingParentGroups |> List.sumBy _.Members.Length} parents =="
    parentsToRemove |> Seq.iter (fun v -> printfn $"  * %s{v.MailAddress}")

    if not dryRun then do!
        parentsToRemove
        |> List.map (fun v -> async {
            do!
                graphClient.Users.[v.UserId].DeleteAsync() |> Async.AwaitTask
                |> GraphServiceClient.formatError $"Error while removing %s{v.MailAddress}"
        })
        |> Async.Parallel
        |> Async.Ignore

    printfn $"== Adding %d{parentsToAdd.Length} parents =="
    parentsToAdd |> Seq.iter (fun v -> printfn $"  * %s{v}")

    let! newParents = async {
        if not dryRun then
            let! result =
                parentsToAdd
                |> List.map (fun v -> async {
                    let! user = graphClient.CreateParentUser v
                    return { UserId = user.InvitedUser.Id; MailAddress = v; UserType = GuestUser }
                })
                |> Async.Parallel
            return List.ofArray result
        else return parentsToAdd |> List.map (fun v -> { UserId = "dry-run-user-id"; MailAddress = v; UserType = GuestUser })
    }

    let allParents = (existingParentGroups |> List.collect _.Members) @ newParents

    let (groupsToAdd, groupsToRemove) =
        List.diff (existingParentGroups, _.Name) (sokratesParentAddresses, _.GroupName)

    printfn $"== Removing %d{groupsToRemove.Length}/%d{existingParentGroups.Length} parent groups =="
    groupsToRemove |> Seq.iter (fun v -> printfn $"  * %s{v.Name}")

    if not dryRun then do!
        groupsToRemove
        |> List.map (fun v -> async {
            do!
                graphClient.Groups.[v.GroupId].DeleteAsync() |> Async.AwaitTask
                |> GraphServiceClient.formatError $"Error while removing %s{v.Name}"
        })
        |> Async.Parallel
        |> Async.Ignore

    printfn $"== Adding %d{groupsToAdd.Length} parent groups =="
    groupsToAdd |> Seq.iter (fun v -> printfn $"  * %s{v.GroupName}")

    let! newGroups = async {
        if not dryRun then
            let! result =
                groupsToAdd
                |> List.map (fun v -> async {
                    let! group = graphClient.CreateParentGroup v.GroupName
                    return { GroupId = group.Id; Name = group.MailNickname; Members = [] }
                })
                |> Async.Parallel
            return List.ofArray result
        else
            return
                groupsToAdd
                |> List.map (fun v ->
                    { GroupId = "dry-run-group-id"; Name = v.GroupName; Members = [] }
                )
    }

    let parentGroups = existingParentGroups @ newGroups
    
    let allParentsByMailAddress =
        allParents
        |> List.except parentsToRemove
        |> List.map (fun v -> v.MailAddress, v)
        |> Map.ofList

    let groupModifications =
        sokratesParentAddresses
        |> List.choose (fun group ->
            let parentGroup =
                parentGroups
                |> List.tryFind (fun v -> v.Name = group.GroupName)
                |> Option.defaultWith (fun () -> failwith $"Can't find %s{group.GroupName}")
            let users =
                group.StudentAddresses
                |> List.map (fun v -> allParentsByMailAddress |> Map.tryFind v |> Option.defaultWith (fun () -> failwith $"Can't find %s{v}"))
            let (usersToAdd, usersToRemove) = List.diff (parentGroup.Members, id) (users, id)
            if usersToAdd.Length > 0 || usersToRemove.Length > 0 then
                let groupDescription =
                    match group.StudentsWithoutAddresses with
                    | [] -> "keine"
                    | v -> v |> String.concat ", "
                    |> sprintf "Fehlende Adressen: %s"
                Some {| Group = parentGroup; UsersToAdd = usersToAdd; UsersToRemove = usersToRemove; GroupDescription = groupDescription |}
            else None
        )

    groupModifications
    |> Seq.iter (fun v ->
        printfn $"== Modifying %s{v.Group.Name} =="
        printfn $"  * Setting description to \"%s{v.GroupDescription}\""
        v.UsersToRemove |> Seq.iter (fun v -> printfn $"  * Removing %s{v.MailAddress}")
        v.UsersToAdd |> Seq.iter (fun v -> printfn $"  * Adding %s{v.MailAddress}")
    )

    if not dryRun then do!
        groupModifications
        |> List.collect (fun m ->
            [
                yield! m.UsersToAdd
                |> List.map (fun v -> async {
                    let memberReference = graphClient.GetDirectoryObjectReference(Models.User(Id = v.UserId))
                    do!
                        graphClient.Groups.[m.Group.GroupId].Members.Ref.PostAsync(memberReference) |> Async.AwaitTask
                        |> GraphServiceClient.formatError $"Error while adding %s{v.MailAddress} to %s{m.Group.Name}"
                })
                yield! m.UsersToRemove
                |> List.map (fun v -> async {
                    let memberReference = graphClient.GetDirectoryObjectReference(Models.User(Id = v.UserId))
                    do!
                        graphClient.Groups.[m.Group.GroupId].Members.[memberReference.OdataId].Ref.DeleteAsync() |> Async.AwaitTask
                        |> GraphServiceClient.formatError $"Error while removing %s{v.MailAddress} from %s{m.Group.Name}"
                })

                async {
                    let patch = Models.Group(Description = m.GroupDescription)
                    do!
                        graphClient.Groups.[m.Group.GroupId].PatchAsync(patch) |> Async.AwaitTask |> Async.Ignore
                        |> GraphServiceClient.formatError $"Error while setting group description of %s{m.Group.Name}"
                }
            ]
        )
        |> Async.Parallel
        |> Async.Ignore

    printfn "== Adding new parent groups to global parent group =="
    newGroups
    |> List.iter (fun v -> printfn $"  * Adding %s{v.Name}")

    if not dryRun then
        let! globalParentsGroup = async {
            let! groups =
                graphClient.Groups.GetAsync(fun v -> v.QueryParameters.Filter <- "displayName eq 'GrpEltern'")
                |> graphClient.ReadAll<_, Models.Group>
            return groups |> Seq.exactlyOne
        }
        do!
            newGroups
            |> List.map (fun v -> async {
                let groupReference = graphClient.GetDirectoryObjectReference(Models.Group(Id = v.GroupId))
                do!
                    graphClient.Groups.[globalParentsGroup.Id].Members.Ref.PostAsync(groupReference) |> Async.AwaitTask
                    |> GraphServiceClient.formatError $"Error while adding %s{v.Name} to %s{globalParentsGroup.DisplayName}"
            })
            |> Async.Parallel
            |> Async.Ignore
}
|> Async.RunSynchronously
