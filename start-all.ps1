$commands =
    @(
        "dapr run --app-id wake-up-computer --app-port 3000 --port 3500 -- dotnet run -p .\src\WakeUpComputer\WakeUpComputer.fsproj --urls=http://+:3000"
        # "dapr run --app-id sokrates --app-port 3001 --port 3501 -- node .\src\Sokrates\server.js 3001"
        "dapr run --app-id sokrates --app-port 3001 --port 3501 -- docker run --rm -v `"$PSScriptRoot\src\Sokrates:/usr/src/app`" -v `"$(Split-Path -Parent $env:SOKRATES_CLIENT_CERTIFICATE_PATH):/usr/cert/`" --env `"SOKRATES_CLIENT_CERTIFICATE_PATH=/usr/cert/cs_edusolutions.pfx`" --env `"SOKRATES_CLIENT_CERTIFICATE_PASSPHRASE=$env:SOKRATES_CLIENT_CERTIFICATE_PASSPHRASE`" -w /usr/src/app -p 3001:3001 node:10 node server.js 3001"
        "dapr run --app-id untis --app-port 3002 --port 3502 -- dotnet run -p .\src\Untis\Untis.fsproj --urls=http://+:3002"
        "dapr run --app-id final-theses --app-port 3003 --port 3503 -- dotnet run -p .\src\FinalTheses\FinalTheses.fsproj --urls=http://+:3003"
        "dapr run --app-id photo-library --app-port 3004 --port 3504 -- dotnet run -p .\src\PhotoLibrary\PhotoLibrary.fsproj --urls=http://+:3004"
        "dapr run --app-id file-storage --app-port 3005 --port 3505 -- dotnet run -p .\src\FileStorage\FileStorage.fsproj --urls=http://+:3005"
        "dapr run --app-id aad --app-port 3006 --port 3506 -- dotnet run -p .\src\AAD\AAD.fsproj --urls=http://+:3006"
        "dapr run --app-id teaching-server --app-port 3007 --port 3507 -- dotnet run -p .\src\Teaching.Server\Teaching.Server.fsproj --urls=http://+:3007"
        "dapr run --app-id management-server --app-port 3008 --port 3508 -- dotnet run -p .\src\Management.Server\Management.Server.fsproj --urls=http://+:3008"
    ) |
    ForEach-Object {
        $cmd = $_ -replace " -- "," `"--`" "
        "new-tab -d $pwd $cmd"
    }
Invoke-Expression "wt $($commands -join "``; ")"
