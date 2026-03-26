#!/usr/bin/env bash
# Teste rápido: login → gera PDF com template inline (sem tabela `templates`).
# Uso: BASE=http://localhost:3000 USER=docengine.demo PASS='DocEngine@2025' ./scripts/test-pdf-inline.sh
set -euo pipefail
BASE="${BASE:-http://localhost:3000}"
USER="${USER:-docengine.demo}"
PASS="${PASS:-DocEngine@2025}"

echo "==> Login em $BASE/auth/login"
# Corpo em ficheiro temp; só o código HTTP vem no stdout do curl (não misturar com stderr do curl)
TMP_LOGIN=$(mktemp)
set +e
LOGIN_HTTP=$(curl -sS -w "%{http_code}" -o "$TMP_LOGIN" --connect-timeout 8 -X POST "$BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"$USER\",\"password\":\"$PASS\"}" 2>/dev/null)
CURL_LOGIN_EXIT=$?
set -e
LOGIN_BODY=$(cat "$TMP_LOGIN" 2>/dev/null || true)
rm -f "$TMP_LOGIN"

if [[ "$CURL_LOGIN_EXIT" -ne 0 ]] || [[ "$LOGIN_HTTP" == "000" ]]; then
  echo "Não foi possível ligar a $BASE (curl exit $CURL_LOGIN_EXIT, http_code=${LOGIN_HTTP:-n/a})."
  echo "  → Inicie a API: cd backend/KYX.DocEngine.API && dotnet run"
  echo "  → Veja a porta na consola («Now listening on http://localhost:XXXX»)."
  echo "  → Outra porta: BASE=http://localhost:PORTA ./scripts/test-pdf-inline.sh"
  exit 1
fi

TOKEN=$(echo "$LOGIN_BODY" | python3 -c "
import sys, json
try:
    d = json.load(sys.stdin)
    r = d.get('resultado') or {}
    print(r.get('access_token') or '')
except Exception:
    pass
" 2>/dev/null || true)

if [[ -z "$TOKEN" ]]; then
  echo "Falha no login (HTTP $LOGIN_HTTP)."
  echo "Corpo:"
  echo "$LOGIN_BODY" | head -c 1200
  echo ""
  if [[ "$LOGIN_HTTP" == "401" ]] || [[ "$LOGIN_HTTP" == "403" ]]; then
    echo "→ Credenciais recusadas. Ajuste USER/PASS ou crie o utilizador (docs/sql/insert-usuario-legado-tb_usuario.sql)."
  fi
  HC=$(curl -sS -o /dev/null -w "%{http_code}" --connect-timeout 2 "$BASE/health" 2>/dev/null || echo "")
  if [[ "$HC" == "200" ]]; then
    echo "→ GET $BASE/health = 200 (API no ar); falha só em /auth/login."
  fi
  exit 1
fi

REQ_ID="test-pdf-$(date +%s)"
echo "==> POST /documents/generate (inlineTemplate)"
RESP=$(curl -s -w "\n%{http_code}" -X POST "$BASE/documents/generate" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "requisicaoId": "'"$REQ_ID"'",
    "config": {
      "centroCusto": "TEST001",
      "nomeArquivo": "teste-docengine.pdf",
      "inlineTemplate": {
        "type": "html",
        "content": "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>body{font-family:system-ui,sans-serif;padding:40px}h1{color:#0d6e8a}</style></head><body><h1>DocEngine — teste PDF</h1><p>Cliente: <strong>{{nome}}</strong></p><p>Data: {{dataDoc}}</p></body></html>",
        "requiredFields": ["nome", "dataDoc"]
      }
    },
    "dados": {
      "nome": "Teste Local",
      "dataDoc": "2026-03-23"
    }
  }')
HTTP=$(echo "$RESP" | tail -n1)
BODY=$(echo "$RESP" | sed '$d')
echo "$BODY" | python3 -m json.tool 2>/dev/null || echo "$BODY"
if [[ "$HTTP" != "200" ]]; then
  echo "HTTP $HTTP — se vir 'Template field is required', recompile e reinicie a API (código novo não exige config.template com inlineTemplate)."
  exit 1
fi

JOB_ID=$(echo "$BODY" | python3 -c "import sys,json; print(json.load(sys.stdin).get('resultado',{}).get('jobId',''))")
if [[ -z "$JOB_ID" ]]; then
  echo "Sem jobId na resposta."
  exit 1
fi
echo "jobId: $JOB_ID"

echo "==> Polling GET /documents/status/$JOB_ID"
for i in $(seq 1 60); do
  ST=$(curl -s "$BASE/documents/status/$JOB_ID" -H "Authorization: Bearer $TOKEN")
  S=$(echo "$ST" | python3 -c "import sys,json; print(json.load(sys.stdin).get('resultado',{}).get('status',''))")
  echo "[$i] status=$S"
  if [[ "$S" == "completed" ]] || [[ "$S" == "failed" ]]; then
    if [[ "$S" == "failed" ]]; then
      echo "$ST" | python3 -m json.tool
      exit 1
    fi
    OUT="${OUT:-./teste-docengine.pdf}"
    echo "$ST" | python3 -c "
import sys, json, base64
d = json.load(sys.stdin)
b = d.get('resultado', {}).get('resultado', {}).get('base64')
if not b:
    sys.exit('sem base64')
open('$OUT', 'wb').write(base64.b64decode(b))
print('PDF gravado em:', '$OUT')
"
    exit 0
  fi
  sleep 2
done
echo "Timeout aguardando job."
exit 1
