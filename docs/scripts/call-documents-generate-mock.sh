#!/usr/bin/env bash
# Chama POST /documents/generate com docs/preview/mock-kit/dossie-simplix-api-request.mock.json
# Exige API em Development com Documents:DevFileTemplateFallback e SkipPartnerDocumentoPersist (appsettings.Development.json).
#
# Uso:
#   Terminal 1: cd backend/KYX.DocEngine.API && ASPNETCORE_ENVIRONMENT=Development dotnet run --urls http://127.0.0.1:3000
#   Terminal 2:
#     export BASE_URL=http://127.0.0.1:3000
#     bash docs/scripts/call-documents-generate-mock.sh
#
# Saída: docs/preview/dossie-generate-api-from-mock.pdf (por defeito; sobrescreve OUT_PDF)
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

: "${BASE_URL:=http://127.0.0.1:3000}"
: "${OUT_PDF:=docs/preview/dossie-generate-api-from-mock.pdf}"
MOCK="$ROOT/docs/preview/mock-kit/dossie-simplix-api-request.mock.json"
USER="${DOCENGINE_USERNAME:-docengine.demo}"
PASS="${DOCENGINE_PASSWORD:-DocEngine@2025}"

LOGIN_JSON=$(curl -sS -X POST "${BASE_URL%/}/auth/login" \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json' \
  -d "{\"username\":\"${USER}\",\"password\":\"${PASS}\"}")

TOKEN=$(python3 -c "import json,sys; j=json.loads(sys.argv[1]); r=j.get('resultado') or j.get('Resultado') or {}; print(r.get('access_token',''))" "$LOGIN_JSON")

if [[ -z "$TOKEN" ]]; then
  echo "Login falhou. Resposta:" >&2
  echo "$LOGIN_JSON" >&2
  exit 1
fi

GEN_JSON=$(curl -sS -X POST "${BASE_URL%/}/documents/generate" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json' \
  --data-binary @"$MOCK")

python3 -c "
import json,sys,base64, pathlib
j=json.loads(sys.argv[1])
if not j.get('sucesso') and not j.get('Sucesso'):
  print(json.dumps(j, indent=2), file=sys.stderr)
  sys.exit(2)
res=j.get('resultado') or j.get('Resultado') or {}
b64=res.get('base64') or res.get('Base64')
if not b64:
  print('Sem base64 na resposta', file=sys.stderr)
  print(json.dumps(j, indent=2), file=sys.stderr)
  sys.exit(3)
path=pathlib.Path(sys.argv[2])
path.parent.mkdir(parents=True, exist_ok=True)
path.write_bytes(base64.b64decode(b64))
print('OK:', path.resolve(), 'bytes:', path.stat().st_size)
" "$GEN_JSON" "$ROOT/$OUT_PDF"

echo "PDF: $ROOT/$OUT_PDF"
