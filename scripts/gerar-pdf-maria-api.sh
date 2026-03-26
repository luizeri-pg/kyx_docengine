#!/usr/bin/env bash
# Login → POST /documents/generate-sync com o body da Maria (mesmo PDF que generate_pdf_maria_ficticio.py).
# Pré-requisitos: API em http://localhost:3000, utilizador docengine.demo / DocEngine@2025, AllowSyncPdfGeneration=true (Development).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BASE="${BASE_URL:-http://localhost:3000}"
BASE="${BASE%/}"
BODY="${MARIA_JSON:-$ROOT/docs/samples/maria-generate-sync-body.json}"
OUT="${OUT:-$ROOT/maria-dados-ficticios.pdf}"

if [[ ! -f "$BODY" ]]; then
  echo "Ficheiro JSON não encontrado: $BODY" >&2
  exit 1
fi

echo "==> POST $BASE/auth/login"
TMP=$(mktemp)
LOGIN_HTTP=$(curl -sS -w "%{http_code}" -o "$TMP" --connect-timeout 10 -X POST "$BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"docengine.demo","password":"DocEngine@2025"}' || true)
LOGIN_BODY=$(cat "$TMP" || true)
rm -f "$TMP"

if [[ "$LOGIN_HTTP" != "200" ]]; then
  echo "Login falhou (HTTP ${LOGIN_HTTP:-erro})." >&2
  echo "$LOGIN_BODY" | head -c 800 >&2
  echo "" >&2
  echo "→ API a correr? → cd backend/KYX.DocEngine.API && dotnet run" >&2
  echo "→ BD + utilizador: docs/SETUP-DO-ZERO.md e docs/sql/insert-usuario-pos-migracoes.sql" >&2
  exit 1
fi

TOKEN=$(echo "$LOGIN_BODY" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['resultado']['access_token'])")

echo "==> POST $BASE/documents/generate-sync (body: $BODY)"
RESP=$(curl -sS -w "\n%{http_code}" -X POST "$BASE/documents/generate-sync" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d @"$BODY")
HTTP=$(echo "$RESP" | tail -n1)
JSON=$(echo "$RESP" | sed '$d')

if [[ "$HTTP" != "200" ]]; then
  echo "generate-sync falhou (HTTP $HTTP):" >&2
  echo "$JSON" | python3 -m json.tool 2>/dev/null || echo "$JSON" >&2
  exit 1
fi

echo "$JSON" | python3 -c "
import sys, json, base64
r = json.load(sys.stdin)
if not r.get('sucesso'):
    print('Erro:', r, file=sys.stderr)
    sys.exit(1)
b64 = r['resultado']['base64']
open('$OUT', 'wb').write(base64.b64decode(b64))
print('PDF gravado em:', '$OUT')
"
