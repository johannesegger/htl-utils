﻿$TenantId = az account show --query "tenantId" -o tsv
$ClientId = az ad app list --display-name HTLUtilsConsoleApp --query "[].appId" -o tsv
dotnet fsi .\send-student-mails.fsx -- $TenantId $ClientId
