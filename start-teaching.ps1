$serverDir = "$PSScriptRoot\src\Teaching.Server"

$clientDir = "$PSScriptRoot\src\Teaching.Client"
yarn --cwd $clientDir install --frozen-lockfile

dotnet tool restore

wt `
    new-tab --title Teaching.Server --startingDirectory $serverDir `-`- dotnet watch run --urls=http://+:3000 `; `
    new-tab --title Teaching.Client --startingDirectory $clientDir `-`- dotnet fable watch .\src --run webpack-dev-server
