#!/usr/bin/env bash
# Gera deploy/docker-stack.env.yml a partir de variáveis de ambiente e faz docker stack deploy.
# Uso: na raiz do repo, com REGISTRY, TAG, STACK_NAME, API_HOST, WEB_HOST, POSTGRES_*, JWT_SECRET_KEY,
#      IMAGE_REPOSITORY_API, IMAGE_REPOSITORY_WEB exportados (e opcionais ConnectionStrings__*, Jwt__*, etc.)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

: "${REGISTRY:?}" "${TAG:?}" "${STACK_NAME:?}" "${API_HOST:?}" "${WEB_HOST:?}"
: "${POSTGRES_USER:?}" "${POSTGRES_PASSWORD:?}" "${POSTGRES_DB:?}" "${JWT_SECRET_KEY:?}"
export IMAGE_REPOSITORY_API="${IMAGE_REPOSITORY_API:-docengine-api}"
export IMAGE_REPOSITORY_WEB="${IMAGE_REPOSITORY_WEB:-docengine-web}"

OVERRIDE_FILE="$ROOT/deploy/docker-stack.env.yml"
: > "$OVERRIDE_FILE"
printf 'version: "3.9"\nservices:\n' >> "$OVERRIDE_FILE"

KEYS=(
  ConnectionStrings__DefaultConnection
  ConnectionStrings__Redis
  Jwt__SecretKey
  Jwt__Issuer
  Jwt__Audience
  Jwt__ExpirationMinutes
  Cors__AllowedOrigins__0
  Cors__AllowedOrigins__1
  Cors__AllowedOrigins__2
  Cors__AllowedOrigins__3
  Cors__AllowedOrigins__4
  Database__ApplyMigrationsOnStartup
  Hangfire__Storage
  Documents__AllowSyncPdfGeneration
  Auth__UseDatabaseForLogin
)

add_env_block () {
  local svc="$1"
  local body=""
  local key val esc_val
  for key in "${KEYS[@]}"; do
    val="${!key-}"
    [[ -z "$val" ]] && continue
    if [[ "$val" == \$\(*\) ]]; then
      continue
    fi
    esc_val=${val//\'/\'\'}
    body="${body}      ${key}: '${esc_val}'\n"
  done
  if [[ -n "$body" ]]; then
    printf "  %s:\n    environment:\n%b" "$svc" "$body" >> "$OVERRIDE_FILE"
  fi
}

add_env_block api

if grep -qE '^  api:' "$OVERRIDE_FILE"; then
  echo "Override env keys (names only):"
  sed -n "s/^[[:space:]]\{6\}\([^:]*\):.*/\1/p" "$OVERRIDE_FILE" | sort -u
  OVERRIDE_ARGS=( -c "$OVERRIDE_FILE" )
else
  echo "No override env keys set."
  OVERRIDE_ARGS=()
fi

docker stack deploy --with-registry-auth -c "$ROOT/deploy/docker-stack.yml" "${OVERRIDE_ARGS[@]}" "${STACK_NAME}"
