wt `
    new-tab --title WakeUpComputer --startingDirectory $pwd dapr run --app-id wake-up-computer --app-port 3000 --port 3500 `-`- dotnet watch -p .\src\WakeUpComputer\WakeUpComputer.fsproj run --urls=http://+:3000 `; `
    new-tab --title Sokrates --startingDirectory $pwd dapr run --app-id sokrates --app-port 3001 --port 3501 `-`- dotnet watch -p .\src\Sokrates\Sokrates.fsproj run --urls=http://+:3001 `; `
    new-tab --title Untis --startingDirectory $pwd dapr run --app-id untis --app-port 3002 --port 3502 `-`- dotnet watch -p .\src\Untis\Untis.fsproj run --urls=http://+:3002 `; `
    new-tab --title FinalTheses --startingDirectory $pwd dapr run --app-id final-theses --app-port 3003 --port 3503 `-`- dotnet watch -p .\src\FinalTheses\FinalTheses.fsproj run --urls=http://+:3003 `; `
    new-tab --title PhotoLibrary --startingDirectory $pwd dapr run --app-id photo-library --app-port 3004 --port 3504 `-`- dotnet watch -p .\src\PhotoLibrary\PhotoLibrary.fsproj run --urls=http://+:3004 `; `
    new-tab --title FileStorage --startingDirectory $pwd dapr run --app-id file-storage --app-port 3005 --port 3505 `-`- dotnet watch -p .\src\FileStorage\FileStorage.fsproj run --urls=http://+:3005 `; `
    new-tab --title AAD --startingDirectory $pwd dapr run --app-id aad --app-port 3006 --port 3506 `-`- dotnet watch -p .\src\AAD\AAD.fsproj run --urls=http://+:3006 `; `
    new-tab --title TeachingServer --startingDirectory $pwd dapr run --app-id teaching-server --app-port 3007 --port 3507 `-`- dotnet watch -p .\src\Teaching.Server\Teaching.Server.fsproj run --urls=http://+:3007 `; `
    new-tab --title ManagementServer --startingDirectory $pwd dapr run --app-id management-server --app-port 3008 --port 3508 `-`- dotnet watch -p .\src\Management.Server\Management.Server.fsproj run --urls=http://+:3008 `; `
    new-tab --title AD --startingDirectory $pwd dapr run --app-id ad --app-port 3009 --port 3509 `-`- dotnet watch -p .\src\AD\AD.fsproj run --urls=http://+:3009