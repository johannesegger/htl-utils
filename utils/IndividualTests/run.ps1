Push-Location $PSScriptRoot

Remove-Item .\out -Force -Recurse -ErrorAction Ignore

$TenantId = az account show --query "tenantId" -o tsv
$ClientId = az ad app list --display-name HTLUtilsConsoleApp --query "[].appId" -o tsv
$StudentsGroupId = az ad group list --filter "displayName eq 'GrpSchueler'" --query "[].id" -o tsv
$SokratesReferenceDates = "2024-07-03,2024-04-26"
$TestFilePath = ".\data\2324-Pruefungen.xlsx"
dotnet run -- $TenantId $ClientId $StudentsGroupId $SokratesReferenceDates $TestFilePath --no-include-room
.\combine.ps1

Pop-Location
