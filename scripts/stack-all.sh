#!/usr/bin/env bash
# Ordem: rede Swarm → build → push → deploy (faz tudo exceto docker login e swarm init).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

swarm_state="$(docker info -f '{{.Swarm.LocalNodeState}}' 2>/dev/null || echo inactive)"
if [[ "$swarm_state" != "active" ]]; then
  echo "Corre primeiro: docker swarm init"
  exit 1
fi

"${ROOT}/scripts/stack-setup-network.sh"
"${ROOT}/scripts/stack-build-push.sh"
"${ROOT}/scripts/stack-deploy.sh"

echo ">>> Tudo feito. DNS: API_HOST e WEB_HOST → Traefik."
