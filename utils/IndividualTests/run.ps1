Push-Location $PSScriptRoot

Remove-Item .\out -Force -Recurse -ErrorAction Ignore

$TenantId = az account show --query "tenantId" -o tsv
$ClientId = az ad app list --display-name HTLUtilsConsoleApp --query "[].appId" -o tsv
$StudentsGroupId = az ad group list --filter "displayName eq 'GrpSchueler'" --query "[].id" -o tsv
$SokratesReferenceDates = "2025-07-01,2024-04-30"
$TestFilePath = ".\data\2425-Erfassung_03.xlsx"
dotnet run -- $TenantId $ClientId $StudentsGroupId $SokratesReferenceDates $TestFilePath --no-include-room
.\combine.ps1

Pop-Location
