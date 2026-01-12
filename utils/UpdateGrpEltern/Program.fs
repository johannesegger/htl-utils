open AAD
open Domain
open Sokrates

let debug = false
let dryRun = true

async {
    let sokratesApi = SokratesApi.FromEnvironment()
    let! sokratesParentAddresses = sokratesApi.GetParentGroups()

    let aadConfig = AAD.Configuration.Config.fromEnvironment ()
    let graphClient =
        GraphServiceClientFactory.createWithAppSecret aadConfig.OidcConfig
        // GraphServiceClientFactory.createWithDeviceCode aadConfig.OidcConfig [| "Directory.ReadWrite.All" |] "HtlUtils.UpdateGrpEltern"

    let! (existingParentGroups: ExistingParentGroup list) = graphClient.GetParentGroups()

    let (parentsToAdd, parentsToRemove) = getParentsDiff sokratesParentAddresses existingParentGroups

    let allParents = (existingParentGroups |> List.collect _.Members |> List.map _.MailAddress) @ parentsToAdd

    let (groupsToAdd, groupsToRemove) =
        List.diff (existingParentGroups, _.Name) (sokratesParentAddresses, _.Name)

    let parentGroups = [
        yield! existingParentGroups |> List.map (fun v -> {| Name = v.Name; Members = v.Members |> List.except parentsToRemove |> List.map _.MailAddress |})
        yield! groupsToAdd |> List.map (fun v -> {| Name = v.Name; Members = v.StudentAddresses |})
    ]

    let groupModifications =
        sokratesParentAddresses
        |> List.choose (fun group ->
            let parentGroup =
                parentGroups
                |> List.tryFind (fun v -> v.Name = group.Name)
                |> Option.defaultWith (fun () -> failwith $"Can't find %s{group.Name}")
            let users = group.StudentAddresses
            let (usersToAdd, usersToRemove) = List.diff (parentGroup.Members, CIString) (users, CIString)
            if usersToAdd.Length > 0 || usersToRemove.Length > 0 then
                let groupDescription =
                    match group.StudentsWithoutAddresses with
                    | [] -> "keine"
                    | v -> v |> String.concat ", "
                    |> sprintf "Fehlende Adressen: %s"
                Some {| Group = parentGroup; UsersToAdd = usersToAdd; UsersToRemove = usersToRemove; GroupDescription = groupDescription |}
            else None
        )

    [
        "<#"
        "Install-Module Microsoft.Graph -Scope CurrentUser -Force"
        "Install-Module ExchangeOnlineManagement -Scope CurrentUser -Force"
        "#>"
        "Connect-MgGraph -Scopes \"User.ReadWrite.All Group.ReadWrite.All Directory.ReadWrite.All\""
        "Connect-ExchangeOnline"
        if parentsToRemove.Length > 0 then
            ""
            $"Write-Host \"Removing %d{parentsToRemove.Length} users...\""
            yield! parentsToRemove
                |> List.sortBy _.MailAddress
                |> List.map (fun v -> $"Remove-MgUser -UserId %s{v.UserId} # %s{v.MailAddress}")
        if parentsToAdd.Length > 0 then
            ""
            $"Write-Host \"Creating %d{parentsToAdd.Length} guest users...\""
            yield! parentsToAdd
                |> List.sort
                |> List.map (fun mailAddress -> $"New-MgInvitation -InvitedUserEmailAddress {mailAddress} -InviteRedirectUrl \"https://www.htlvb.at\" | Out-Null")
        if groupsToRemove.Length > 0 then
            ""
            $"Write-Host \"Removing %d{groupsToRemove.Length} parent groups...\""
            yield! groupsToRemove
                |> List.sortBy _.Name
                |> List.map (fun v -> $"Remove-MgGroup -GroupId %s{v.GroupId} # %s{v.Name}")
        if groupsToAdd.Length > 0 then
            ""
            $"Write-Host \"Adding %d{groupsToAdd.Length} parent groups...\""
            yield! groupsToAdd
                |> List.sortBy _.Name
                |> List.collect (fun v -> [
                    $"$Group = New-DistributionGroup -Name %s{v.Name} -Alias %s{v.Name} -PrimarySmtpAddress \"%s{v.Name}@htlvb.at\" -ManagedBy admin@htlvb.at -MemberJoinRestriction Closed -MemberDepartRestriction Closed"
                    "Set-DistributionGroup $Group.Identity -AcceptMessagesOnlyFromSendersOrMembers GrpLehrer@htlvb.at"
                ])
        if groupModifications.Length > 0 then
            ""
            yield! groupModifications
                |> List.sortBy _.Group.Name
                |> List.collect (fun v -> [
                    $"Write-Host \"Modifying %s{v.Group.Name}...\""
                    $"$Group = Get-MgGroup -Filter \"MailNickname eq '{v.Group.Name}'\""
                    $"Set-DistributionGroup $Group.Id -Description \"%s{v.GroupDescription}\""
                    yield! v.UsersToRemove
                        |> List.sort
                        |> List.map (fun u ->
                            [
                                $"$User = Get-User -Filter \"EmailAddresses -eq '%s{u}'\""
                                $"Remove-DistributionGroupMember $Group.Id -Member $User.Id -Confirm:$false"
                            ]
                            |> String.concat "; "
                        )
                    yield! v.UsersToAdd
                        |> List.sort
                        |> List.map (fun u ->
                            [
                                $"$User = Get-User -Filter \"EmailAddresses -eq '{u}'\""
                                $"Add-DistributionGroupMember $Group.Id -Member $User.Id"
                            ]
                            |> String.concat "; "
                        )
                ])
        if groupsToAdd.Length > 0 then
            ""
            $"Write-Host \"Adding %d{groupsToAdd.Length} new parent groups to global parent group...\""
            yield! groupsToAdd
                |> List.sortBy _.Name
                |> List.map (fun v ->
                    $"Add-DistributionGroupMember GrpEltern -Member \"%s{v.Name}@htlvb.at\""
                )
        ""
        "Write-Host \"Done.\""
    ]
    |> fun lines -> System.IO.File.WriteAllLines("update-grp-eltern.ps1", lines)
}
|> Async.RunSynchronously
