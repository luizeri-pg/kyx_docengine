#!/usr/bin/env bash
# Cria a rede overlay que o Traefik e o compose.stack.yaml esperam.
set -euo pipefail

if ! docker info 2>/dev/null | grep -q "Swarm: active"; then
  echo "Swarm não está ativo. Corre primeiro: docker swarm init"
  exit 1
fi

if docker network inspect traefik_public &>/dev/null; then
  echo "Rede traefik_public já existe."
  exit 0
fi

echo "A criar rede overlay traefik_public..."
docker network create -d overlay traefik_public
echo "OK."
