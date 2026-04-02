#!/usr/bin/env bash
# Replica o publish da API como no Azure (um único docker run sdk:10.0).
# Uso na raiz do repo: ./scripts/publish-api-docker.sh
# Requer Docker. Saída: backend/KYX.DocEngine.API/publish/
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
IMAGE="${DOTNET_SDK_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"

docker run --rm \
  -v "$ROOT:/src" \
  -w /src/backend/KYX.DocEngine.API \
  -e DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
  -e DOTNET_NOLOGO=1 \
  -e NUGET_XMLDOC_MODE=skip \
  -e DOTNET_gcServer=0 \
  "$IMAGE" \
  bash -c 'dotnet restore KYX.DocEngine.API.csproj --verbosity minimal --disable-parallel && dotnet publish KYX.DocEngine.API.csproj -c Release -o ./publish --no-restore -v minimal /p:MaxCpuCount=1 /p:BuildInParallel=false /p:RunAnalyzers=false /p:UseSharedCompilation=false'

echo ">>> OK: $ROOT/backend/KYX.DocEngine.API/publish"
