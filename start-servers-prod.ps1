dotnet publish .\src\WakeUpComputer -c Release -o .\deploy\WakeUpComputer
dotnet publish .\src\Sokrates -c Release -o .\deploy\Sokrates
dotnet publish .\src\Untis -c Release -o .\deploy\Untis
dotnet publish .\src\FinalTheses -c Release -o .\deploy\FinalTheses
dotnet publish .\src\PhotoLibrary -c Release -o .\deploy\PhotoLibrary
dotnet publish .\src\FileStorage -c Release -o .\deploy\FileStorage
dotnet publish .\src\AAD -c Release -o .\deploy\AAD
dotnet publish .\src\Teaching.Server -c Release -o .\deploy\Teaching
dotnet publish .\src\Management.Server -c Release -o .\deploy\Management
dotnet publish .\src\AD -c Release -o .\deploy\AD

yarn --cwd .\src\Teaching.Client webpack --output-path "$pwd\deploy\Teaching\wwwroot"
yarn --cwd .\src\Management.Client webpack --output-path "$pwd\deploy\Management\wwwroot"

wt `
    new-tab --title WakeUpComputer --startingDirectory .\deploy\WakeUpComputer dapr run --app-id wake-up-computer --app-port 3000 --port 3500 `-`- .\WakeUpComputer.exe --urls=http://+:3000 `; `
    new-tab --title Sokrates --startingDirectory .\deploy\Sokrates dapr run --app-id sokrates --app-port 3001 --port 3501 `-`- .\Sokrates.exe --urls=http://+:3001 `; `
    new-tab --title Untis --startingDirectory .\deploy\Untis dapr run --app-id untis --app-port 3002 --port 3502 `-`- .\Untis.exe --urls=http://+:3002 `; `
    new-tab --title FinalTheses --startingDirectory .\deploy\FinalTheses dapr run --app-id final-theses --app-port 3003 --port 3503 `-`- .\FinalTheses.exe --urls=http://+:3003 `; `
    new-tab --title PhotoLibrary --startingDirectory .\deploy\PhotoLibrary dapr run --app-id photo-library --app-port 3004 --port 3504 `-`- .\PhotoLibrary.exe --urls=http://+:3004 `; `
    new-tab --title FileStorage --startingDirectory .\deploy\FileStorage dapr run --app-id file-storage --app-port 3005 --port 3505 `-`- .\FileStorage.exe --urls=http://+:3005 `; `
    new-tab --title AAD --startingDirectory .\deploy\AAD dapr run --app-id aad --app-port 3006 --port 3506 `-`- .\AAD.exe --urls=http://+:3006 `; `
    new-tab --title Teaching --startingDirectory .\deploy\Teaching dapr run --app-id teaching-server --app-port 3007 --port 3507 `-`- .\Teaching.Server.exe --urls=http://+:3007 `; `
    new-tab --title Management --startingDirectory .\deploy\Management dapr run --app-id management-server --app-port 3008 --port 3508 `-`- .\Management.Server.exe --urls=http://+:3008 `; `
    new-tab --title AD --startingDirectory .\deploy\AD dapr run --app-id ad --app-port 3009 --port 3509 `-`- .\AD.exe --urls=http://+:3009
