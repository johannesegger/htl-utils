Push-Location $PSScriptRoot

Remove-Item .\out -Force -Recurse -ErrorAction Ignore

$TenantId = az account show --query "tenantId" -o tsv
$ClientId = az ad app list --display-name HTLUtilsConsoleApp --query "[].appId" -o tsv
$StudentsGroupId = az ad group list --filter "displayName eq 'GrpSchueler'" --query "[].id" -o tsv
$SokratesReferenceDates = "2025-07-01,2025-04-30"
$TestFilePath = ".\data\2425-Erfassung_06.xlsx"
dotnet run -- $TenantId $ClientId $StudentsGroupId $SokratesReferenceDates $TestFilePath --no-include-room

Pop-Location
