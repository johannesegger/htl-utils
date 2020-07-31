$cwd = "$PSScriptRoot\src\Teaching.Client"
yarn --cwd $cwd install --frozen-lockfile
yarn --cwd $cwd webpack-dev-server
