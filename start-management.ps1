$serverDir = "$PSScriptRoot\src\Management.Server"

$clientDir = "$PSScriptRoot\src\Management.Client"
yarn --cwd $clientDir install --frozen-lockfile

dotnet tool restore

wt `
    new-tab --title Management.Server --startingDirectory $serverDir `-`- dotnet watch run --urls=http://+:3001`; `
    new-tab --title Management.Client --startingDirectory $clientDir `-`- dotnet fable watch .\src --run webpack-dev-server
