$teachingDir = "$PSScriptRoot\deploy\Teaching"
dotnet publish .\src\Teaching.Server -c Release -o $teachingDir
yarn --cwd .\src\Teaching.Client install --frozen-lockfile
yarn --cwd .\src\Teaching.Client webpack --output-path "$teachingDir\wwwroot"

$managementDir = "$PSScriptRoot\deploy\Management"
dotnet publish .\src\Management.Server -c Release -o $managementDir
yarn --cwd .\src\Management.Client install --frozen-lockfile
dotnet tool restore
dotnet fable .\src\Management.Client\src --run yarn --cwd .\src\Management.Client webpack --output-path "$managementDir\wwwroot"

dotnet publish .\src\QueryComputerInfo.Service -c Release -o "$PSScriptRoot\deploy\QueryComputerInfo"
