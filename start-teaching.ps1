$serverDir = "$PSScriptRoot\src\Teaching.Server"

$clientDir = "$PSScriptRoot\src\Teaching.Client"
yarn --cwd $clientDir install --frozen-lockfile

wt `
    new-tab --title Teaching.Server --startingDirectory $serverDir `-`- dotnet watch run --urls=http://+:3000 `; `
    new-tab --title Teaching.Client --startingDirectory $clientDir `-`- yarn.cmd webpack-dev-server `
