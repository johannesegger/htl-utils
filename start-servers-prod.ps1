dotnet publish .\src\Teaching.Server -c Release -o "$PSScriptRoot\deploy\Teaching"
dotnet publish .\src\Management.Server -c Release -o "$PSScriptRoot\deploy\Management"

yarn --cwd .\src\Teaching.Client webpack --output-path "$PSScriptRoot\deploy\Teaching\wwwroot"
yarn --cwd .\src\Management.Client webpack --output-path "$PSScriptRoot\deploy\Management\wwwroot"

wt `
    new-tab --title Teaching --startingDirectory .\deploy\Teaching `-`- .\Teaching.Server.exe --urls=http://+:3000 `; `
    new-tab --title Management --startingDirectory .\deploy\Management `-`- .\Management.Server.exe --urls=http://+:3001 `
