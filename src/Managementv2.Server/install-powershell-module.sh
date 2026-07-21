#!/usr/bin/env bash

set -euo pipefail

if [ $# -lt 1 ]; then
    echo "Usage: $0 MODULE_NAME" >&2
    exit 1
fi

MODULE_NAME="$1"
MODULES_DIR="${MODULES_DIR:-./powershell-modules}"
PWSH_IMAGE="${PWSH_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"

mkdir -p "$MODULES_DIR"

docker run --rm -v "$(realpath "$MODULES_DIR"):/modules" "$PWSH_IMAGE" \
    pwsh -NoProfile -Command \
    "Save-PSResource -Name '$MODULE_NAME' -Path /modules -Repository PSGallery -TrustRepository"
