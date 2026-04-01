#!/usr/bin/env bash
# docker stack deploy com variáveis do .env (carregadas para o compose substituir ${VAR}).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

swarm_state="$(docker info -f '{{.Swarm.LocalNodeState}}' 2>/dev/null || echo inactive)"
if [[ "$swarm_state" != "active" ]]; then
  echo "Swarm não está ativo. Corre: docker swarm init"
  exit 1
fi

if [[ ! -f .env ]]; then
  echo "Falta .env — copia env.stack.example para .env e preenche."
  exit 1
fi

set -a
# shellcheck disable=SC1091
source .env
set +a

STACK_DEPLOY_NAME="${STACK_DEPLOY_NAME:-docengine}"

required=(REGISTRY TAG STACK_NAME API_HOST WEB_HOST POSTGRES_USER POSTGRES_PASSWORD POSTGRES_DB JWT_SECRET_KEY)
for v in "${required[@]}"; do
  if [[ -z "${!v:-}" ]]; then
    echo "Variável obrigatória em falta no .env: $v"
    exit 1
  fi
done

if ! docker network inspect traefik_public &>/dev/null; then
  echo "Rede traefik_public não existe. Corre: ./scripts/stack-setup-network.sh"
  exit 1
fi

echo ">>> Deploy stack: ${STACK_DEPLOY_NAME}"
docker stack deploy -c compose.stack.yaml "${STACK_DEPLOY_NAME}"

echo ">>> Serviços:"
docker stack services "${STACK_DEPLOY_NAME}"
