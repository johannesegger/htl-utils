$env:DOTNET_ROOT=Split-Path -Parent (Get-Command dotnet).Path
$env:SISDB_CONNECTION_STRING="Server=localhost;Port=8081;Database=sis2;User=root;Password=1234"
$env:SSL_CERT_PATH="$pwd\test\ssl\cert.pfx"
$env:SSL_CERT_PASSWORD="1234"
$env:CREATE_DIRECTORIES_BASE_DIRECTORIES="X:;$PSScriptRoot\testX;Y:;$PSScriptRoot\testY"
$env:TEACHER_IMAGE_DIR="$PSScriptRoot\test\teacher-images"
. .\set-secrets.ps1
