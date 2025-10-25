function GetResourceAccessDefinition
{
    param(
        $servicePrincipal,
        [string[]]$delegatedPermissions
    )
    @{
        resourceAppId = $servicePrincipal.appId
        resourceAccess = @(
            $delegatedPermissions | ForEach-Object {
                $permissionName = $_
                $permission = $servicePrincipal.oauth2Permissions | Where-Object { $_.value -eq $permissionName }
                @{
                    id = $permission.Id
                    type = "Scope"
                }
            }
        )
    }
}

function ClearOAuth2Permissions
{
    param(
        $app
    )
    $app.oauth2Permissions | ForEach-Object { $_.isEnabled = $false }
    ConvertTo-Json -Compress -Depth 10 @($app.oauth2Permissions) | az ad app update --id $app.objectId --set "oauth2Permissions=@-"
    az ad app update --id $app.objectId --set oauth2Permissions=[]
    $app.oauth2Permissions = @()
}

Write-Host "Signing in with admin@htlvb.at"
$account = az login --username admin@htlvb.at --tenant htlvb.at | ConvertFrom-Json
$user = az ad user show --id admin@htlvb.at | ConvertFrom-Json

Write-Host "# Creating service application"
$serviceApp = az ad app create --display-name HTLUtilsService | ConvertFrom-Json
az ad app update --id $serviceApp.objectId --set oauth2AllowIdTokenImplicitFlow=false
$serviceAppKey = az ad app credential reset --id $serviceApp.objectId --years 2 | ConvertFrom-Json
az ad app update --id $serviceApp.objectId --identifier-uris "api://$($serviceApp.appId)"
$serviceServicePrincipal = az ad sp create --id $serviceApp.objectId | ConvertFrom-Json
az ad app owner add --id $serviceApp.objectId --owner-object-id $user.objectId

Write-Host "## Adding permissions"
$microsoftGraphServicePrincipal = az ad sp show --id "https://graph.microsoft.com" | ConvertFrom-Json
$servicePermissions = @(
    GetResourceAccessDefinition -servicePrincipal $microsoftGraphServicePrincipal -delegatedPermissions offline_access, Calendars.ReadWrite, Contacts.ReadWrite, Directory.Read.All, Group.ReadWrite.All, User.Read.All
)
ConvertTo-Json -Compress -Depth 10 $servicePermissions | az ad app update --id $serviceApp.objectId --required-resource-accesses "@-"

az ad app permission grant --id $serviceApp.appId --api $microsoftGraphServicePrincipal.appId --scope "Directory.Read.All Group.ReadWrite.All User.Read.All" --output none

Write-Host "## Creating app roles"
$serviceAppRoles = @(
    @{
        allowedMemberTypes = @("User")
        description = "Administrators can manage AAD groups and users"
        displayName = "Admin"
        isEnabled = "true"
        value = "admin"
    }
    @{
        allowedMemberTypes = @("User")
        description = "Teachers can read information from other teachers and students"
        displayName = "Teacher"
        isEnabled = "true"
        value = "teacher"
    }
    @{
        allowedMemberTypes = @("User")
        description = "Can send individual test letters"
        displayName = "IndividualTests.LetterSender"
        isEnabled = "true"
        value = "IndividualTests.LetterSender"
    }
    @{
        allowedMemberTypes = @("User")
        description = "Can manage guest accounts"
        displayName = "GuestAccounts.Manager"
        isEnabled = "true"
        value = "GuestAccounts.Manager"
    }
)
ConvertTo-Json -Compress -Depth 10 $serviceAppRoles | az ad app update --id $serviceApp.objectId --app-roles "@-"

Write-Host "## Assigning users and groups to app roles"
Start-Sleep -Seconds 10
$teacherRoleAssignment = @{
    principalId = az ad group show --group GrpLehrer --query "objectId" | ConvertFrom-Json
    resourceId = $serviceServicePrincipal.objectId
    appRoleId = az ad app show --id $serviceApp.objectId --query "appRoles[?value=='teacher'] | [0].id" | ConvertFrom-Json
}
ConvertTo-Json -Compress -Depth 10 $teacherRoleAssignment | az rest --method post --url https://graph.microsoft.com/v1.0/servicePrincipals/$($serviceServicePrincipal.objectId)/appRoleAssignedTo --body "@-" --headers "Content-Type=application/json" --output none

$adminObjectIds = @(
    az ad user show --id admin@htlvb.at --query "objectId" | ConvertFrom-Json
    az ad user show --id eggj@htlvb.at --query "objectId" | ConvertFrom-Json
    az ad user show --id pacr@htlvb.at --query "objectId" | ConvertFrom-Json
)
foreach ($adminObjectId in $adminObjectIds) {
    $adminRoleAssignment = @{
        principalId = $adminObjectId
        resourceId = $serviceServicePrincipal.objectId
        appRoleId = az ad app show --id $serviceApp.objectId --query "appRoles[?value=='admin'] | [0].id" | ConvertFrom-Json
    }
    ConvertTo-Json -Compress -Depth 10 $adminRoleAssignment | az rest --method post --url https://graph.microsoft.com/v1.0/servicePrincipals/$($serviceServicePrincipal.objectId)/appRoleAssignedTo --body "@-" --headers "Content-Type=application/json" --output none
}

Write-Host "# Creating client application"
$clientApp = az ad app create --display-name HTLUtilsClient --reply-urls "http://localhost:9000" | ConvertFrom-Json

az ad app update --id $clientApp.objectId --set oauth2AllowIdTokenImplicitFlow=false

ClearOAuth2Permissions $clientApp

# There is currently no other way to set the type of the reply url
Start-Sleep -Seconds 10
$clientSettings = @{
    spa = @{
        redirectUris = @("http://localhost:9000", "https://bro", "https://htlvb-htlutils")
    }
}
ConvertTo-Json -Compress -Depth 10 $clientSettings | az rest --method patch --uri "https://graph.microsoft.com/v1.0/applications/$($clientApp.objectId)" --body "@-" --headers "Content-Type=application/json" --output none

$clientServicePrincipal = az ad sp create --id $clientApp.objectId | ConvertFrom-Json
az ad app owner add --id $clientApp.objectId --owner-object-id $user.objectId

Write-Host "## Adding permissions"
$permissions = @(
    GetResourceAccessDefinition -servicePrincipal $serviceServicePrincipal -delegatedPermissions user_impersonation
)
ConvertTo-Json -Compress -Depth 10 $permissions | az ad app update --id $clientApp.objectId --required-resource-accesses "@-"

az ad app permission grant --id $clientApp.appId --api $serviceServicePrincipal.appId --scope user_impersonation --output none

Write-Host "## Linking client and service application"
az ad app update --id $serviceApp.objectId --add knownClientApplications $clientApp.appId

Write-Host "Allowing public client flows for automatic tests"
$serviceSettings = @{
    isFallbackPublicClient = $true
}
ConvertTo-Json -Compress -Depth 10 $serviceSettings | az rest --method patch --url https://graph.microsoft.com/v1.0/applications/$($serviceApp.objectId) --headers "Content-Type=application/json" --body '{"isFallbackPublicClient": true}' --output none

Write-Host "# Showing summary"
Write-Host "* Tenant id: $($account.tenantId)"
Write-Host "* Service app id: $($serviceApp.appId)"
Write-Host "* Service app secret: $($serviceAppKey.password)"
Write-Host "* Client app id: $($clientApp.appId)"
