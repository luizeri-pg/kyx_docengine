#!/usr/bin/env bash
# Grava ou atualiza o template na DocEngine (POST /templates ou PUT /templates/{id}).
#
# HTML e requiredFields: por defeito docs/preview/template-dossie-simplix-upsert.json
# (gerado com: node docs/scripts/build-template-dossie-upsert-payload.mjs).
#
# Se o slug já existir, tenta PUT com o id devolvido por GET /templates (UPSERT=1 por defeito).
# O PUT executa UPDATE direto em tb_template — o utilizador da BD precisa de permissão; caso contrário,
# peça ao DBA GRANT UPDATE … ou use só ins_tb_template (POST em slug novo / outro ambiente).
#
# Autenticação: TOKEN=… ou DOCENGINE_USERNAME + DOCENGINE_PASSWORD
# Variáveis: BASE_URL, TEMPLATE_JSON, UPSERT=0 para desligar o fallback PUT
set -euo pipefail
BASE_URL="${BASE_URL:-http://127.0.0.1:3000}"
UPSERT="${UPSERT:-1}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DEFAULT_JSON="$ROOT/docs/preview/template-dossie-simplix-upsert.json"
JSON="${TEMPLATE_JSON:-$DEFAULT_JSON}"

if [[ ! -f "$JSON" ]]; then
  echo "Ficheiro de template inexistente: $JSON" >&2
  echo "Gere com: node docs/scripts/build-template-dossie-upsert-payload.mjs" >&2
  exit 1
fi

resolve_token() {
  if [[ -n "${TOKEN:-}" ]]; then
    return 0
  fi
  if [[ -z "${DOCENGINE_USERNAME:-}" || -z "${DOCENGINE_PASSWORD:-}" ]]; then
    echo "Defina TOKEN=… ou DOCENGINE_USERNAME e DOCENGINE_PASSWORD (ex.: utilizador docengine.demo)." >&2
    exit 1
  fi
  local login_body
  login_body="$(
    DOCENGINE_USERNAME="$DOCENGINE_USERNAME" DOCENGINE_PASSWORD="$DOCENGINE_PASSWORD" python3 -c '
import json, os
print(json.dumps({"username": os.environ["DOCENGINE_USERNAME"], "password": os.environ["DOCENGINE_PASSWORD"]}))
'
  )"
  local resp
  resp="$(curl -sS -X POST "${BASE_URL}/auth/login" \
    -H "Content-Type: application/json" \
    -H "Accept: application/json" \
    -d "${login_body}")"
  TOKEN="$(
    printf '%s' "$resp" | python3 -c '
import json, sys
d = json.load(sys.stdin)
if not d.get("sucesso"):
    sys.stderr.write("Login falhou: " + json.dumps(d, ensure_ascii=False) + "\n")
    sys.exit(1)
r = d.get("resultado") or {}
t = r.get("access_token")
if not t:
    sys.stderr.write("Resposta sem access_token: " + json.dumps(d, ensure_ascii=False) + "\n")
    sys.exit(1)
print(t)
'
  )"
}

resolve_token

tmp="$(mktemp)"
code="$(curl -sS -o "$tmp" -w "%{http_code}" -X POST "${BASE_URL}/templates" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -d @"${JSON}")"
body="$(cat "$tmp")"
rm -f "$tmp"

post_ok=0
if [[ "$code" == "200" ]]; then
  if printf '%s' "$body" | python3 -c 'import json,sys; d=json.load(sys.stdin); sys.exit(0 if d.get("sucesso") else 1)' 2>/dev/null; then
    post_ok=1
  fi
fi

if [[ "$post_ok" == "1" ]]; then
  printf '%s\n' "$body"
  exit 0
fi

exists=0
if printf '%s' "$body" | grep -q 'já existe'; then
  exists=1
fi
if [[ "$UPSERT" == "1" && "$exists" == "1" ]]; then
  slug="$(python3 -c "import json; print(json.load(open(\"$JSON\"))[\"slug\"])")"
  list_tmp="$(mktemp)"
  curl -sS -o "$list_tmp" "${BASE_URL}/templates" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "Accept: application/json"
  tid="$(python3 -c "import json,sys
slug=json.load(open(sys.argv[1]))['slug']
d=json.load(open(sys.argv[2]))
for t in (d.get('resultado') or []):
  if (t.get('slug') or '').strip()==slug.strip():
    print(t.get('id') or '')
    break
" "$JSON" "$list_tmp")"
  rm -f "$list_tmp"
  if [[ -z "$tid" ]]; then
    echo "POST falhou e não encontrei o template na lista para slug=$slug" >&2
    printf '%s\n' "$body" >&2
    exit 1
  fi
  echo "Slug já existente — a tentar PUT /templates/${tid} …" >&2
  put_tmp="$(mktemp)"
  put_code="$(curl -sS -o "$put_tmp" -w "%{http_code}" -X PUT "${BASE_URL}/templates/${tid}" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "Content-Type: application/json" \
    -H "Accept: application/json" \
    -d @"${JSON}")"
  put_body="$(cat "$put_tmp")"
  rm -f "$put_tmp"
  if [[ "$put_code" == "200" ]] && printf '%s' "$put_body" | python3 -c 'import json,sys; d=json.load(sys.stdin); sys.exit(0 if d.get("sucesso") else 1)' 2>/dev/null; then
    printf '%s\n' "$put_body"
    exit 0
  fi
  echo "PUT falhou (HTTP ${put_code}). Em muitos ambientes o utilizador da API pode chamar ins_tb_template (POST) mas não tem UPDATE em tb_template." >&2
  echo "Soluções: GRANT UPDATE na tb_template; ou registe outro slug; ou peça uma função partner tipo upd_tb_template." >&2
  printf '%s\n' "$put_body" >&2
  exit 1
fi

printf '%s\n' "$body" >&2
exit 1
