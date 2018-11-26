docker run `
    -e SSL_CERT_PATH="/ssl/$(Split-Path -Leaf $env:SSL_CERT_PATH)" `
    -e SSL_CERT_PASSWORD="$env:SSL_CERT_PASSWORD" `
    -e SISDB_CONNECTION_STRING="$env:SISDB_CONNECTION_STRING" `
    -e CREATE_DIRECTORIES_BASE_DIRECTORIES="X:;/base-dirs/x;Y:;/base-dirs/y" `
    -e TEACHER_IMAGE_DIR="/images/teachers" `
    -e APP_KEY="$env:APP_KEY" `
    -v "$env:TEACHER_IMAGE_DIR:/images/teachers" `
    -v "$PSScriptRoot\test\testX:/base-dirs/x" `
    -v "$PSScriptRoot\test\testY:/base-dirs/y" `
    -v "$(Split-Path -Parent $env:SSL_CERT_PATH):/ssl" `
    -p 1100:8085 `
    -p 2100:8086 `
    --name htl-utils `
    johannesegger/htl-utils
