$teachingDir = "$PSScriptRoot\deploy\Teaching"
# dotnet publish .\src\Teaching.Server -c Release -o $teachingDir
# yarn --cwd .\src\Teaching.Client webpack --output-path "$teachingDir\wwwroot"

$managementDir = "$PSScriptRoot\deploy\Management"
# dotnet publish .\src\Management.Server -c Release -o $managementDir
# yarn --cwd .\src\Management.Client webpack --output-path "$managementDir\wwwroot"

wt `
    new-tab --title Teaching --startingDirectory $teachingDir `-`- "$teachingDir\Teaching.Server.exe" --urls=http://+:3000 `; `
    new-tab --title Management --startingDirectory $managementDir `-`- "$managementDir\Management.Server.exe" --urls=http://+:3001
