Push-Location $PSScriptRoot

Remove-Item .\out -Force -Recurse -ErrorAction Ignore

$TenantId = az account show --query "tenantId" -o tsv
$ClientId = az ad app list --display-name HTLUtilsConsoleApp --query "[].appId" -o tsv
$StudentsGroupId = az ad group list --filter "displayName eq 'GrpSchueler'" --query "[].id" -o tsv
$SokratesReferenceDate = "2023-07-07"
$TestFilePath = ".\data\2223-Pruefungen.xlsx"
dotnet run -- $TenantId $ClientId $StudentsGroupId $SokratesReferenceDate $TestFilePath
.\convert.ps1
.\combine.ps1

Pop-Location
