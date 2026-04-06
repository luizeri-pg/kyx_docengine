#!/usr/bin/env bash
# Redeploy do stack DocEngine (dev) no Swarm — corre no manager (ex.: server31).
#
# Pré-requisitos: docker login no REGISTRY; este repo na versão certa (git pull)
#                 ou pelo menos deploy/docker-stack.yml atualizado.
#
# Exemplo:
#   export REGISTRY=registry.kubernetes.partner1.com.br
#   export TAG=12345                    # Build.BuildId da imagem no ACR, ou: latest
#   export STACK_NAME=docengine-dev
#   export API_HOST=dev-api-docengine.partner1.com.br
#   export WEB_HOST=dev-app-docengine.partner1.com.br
#   export CONNECTION_STRING_DEFAULT='Host=...;...'
#   export JWT_SECRET_KEY='...'
#   export SWAGGER_ENABLED=true           # opcional
#   ./deploy/redeploy-dev.sh
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

: "${REGISTRY:?}" "${TAG:?}" "${STACK_NAME:?}" "${API_HOST:?}" "${WEB_HOST:?}"
: "${CONNECTION_STRING_DEFAULT:?}" "${JWT_SECRET_KEY:?}"

export SWAGGER_ENABLED="${SWAGGER_ENABLED:-true}"

docker stack deploy --with-registry-auth \
  -c "$ROOT/deploy/docker-stack.yml" \
  "$STACK_NAME"

echo "--- Serviços ---"
docker stack services "$STACK_NAME" || true
echo "--- tasks API ---"
docker service ps "${STACK_NAME}_api" --no-trunc 2>/dev/null || true
echo "--- tasks frontend ---"
docker service ps "${STACK_NAME}_frontend" --no-trunc 2>/dev/null || true
