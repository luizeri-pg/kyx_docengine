#!/usr/bin/env bash
# Build e push das imagens docengine-api e docengine-web.
# Antes: docker login no teu registry; na raiz do repo: cp env.stack.example .env e preenche.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

if [[ ! -f .env ]]; then
  echo "Falta o ficheiro .env na raiz do repo."
  echo "  cp env.stack.example .env"
  echo "  # edita .env com REGISTRY, TAG, API_HOST, WEB_HOST, passwords, JWT"
  exit 1
fi

set -a
# shellcheck disable=SC1091
source .env
set +a

required=(REGISTRY TAG STACK_NAME API_HOST WEB_HOST POSTGRES_USER POSTGRES_PASSWORD POSTGRES_DB JWT_SECRET_KEY)
for v in "${required[@]}"; do
  if [[ -z "${!v:-}" ]]; then
    echo "Variável obrigatória em falta no .env: $v"
    exit 1
  fi
done

if [[ ${#JWT_SECRET_KEY} -lt 32 ]]; then
  echo "AVISO: JWT_SECRET_KEY deve ter pelo menos 32 caracteres em produção."
fi

echo ">>> Build api: ${REGISTRY}/docengine-api:${TAG}"
docker build -t "${REGISTRY}/docengine-api:${TAG}" backend/KYX.DocEngine.API

echo ">>> Build web (VITE_API_URL=https://${API_HOST})"
docker build --build-arg "VITE_API_URL=https://${API_HOST}" -t "${REGISTRY}/docengine-web:${TAG}" frontend

if [[ "${SKIP_PUSH:-0}" == "1" ]]; then
  echo ">>> SKIP_PUSH=1 — sem docker push (imagens ficam só neste Docker)."
else
  echo ">>> Push"
  docker push "${REGISTRY}/docengine-api:${TAG}"
  docker push "${REGISTRY}/docengine-web:${TAG}"
fi

echo ">>> Concluído."
