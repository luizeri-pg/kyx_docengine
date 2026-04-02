#!/usr/bin/env bash
# Replica o publish da API como no CI: SDK Alpine, 2x docker run, -r linux-musl-x64 (runtime Alpine)
# (compatível com imagem aspnet Debian). Uso na raiz: ./scripts/publish-api-docker.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
IMAGE="${DOTNET_SDK_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0-alpine}"
NUGET_CACHE="${NUGET_CACHE:-${TMPDIR:-/tmp}/docengine-nuget}"
mkdir -p "$NUGET_CACHE"

run() {
  docker run --rm \
    -v "$ROOT:/src" \
    -v "$NUGET_CACHE:/nuget" \
    -w /src/backend/KYX.DocEngine.API \
    -e NUGET_PACKAGES=/nuget \
    -e DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    -e DOTNET_NOLOGO=1 \
    -e NUGET_XMLDOC_MODE=skip \
    -e DOTNET_gcServer=0 \
    -e DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    "$IMAGE" \
    sh -c "$1"
}

run 'dotnet restore KYX.DocEngine.API.csproj --verbosity minimal --disable-parallel'
run 'dotnet publish KYX.DocEngine.API.csproj -c Release -o ./publish -r linux-musl-x64 --self-contained false --no-restore -v minimal /p:MaxCpuCount=1 /p:BuildInParallel=false /p:RunAnalyzers=false /p:UseSharedCompilation=false'

echo ">>> OK: $ROOT/backend/KYX.DocEngine.API/publish"
