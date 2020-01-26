docker-compose build

Push-Location .\src\Management.Client
yarn webpack
Pop-Location

Push-Location .\src\Teaching.Client
yarn webpack
Pop-Location