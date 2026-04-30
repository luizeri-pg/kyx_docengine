#!/usr/bin/env bash
# Gera o PDF do dossiê com o mock estruturado.
# 1) Tenta POST /documents/generate com template dossie-simplix-v2 (HTML na BD).
# 2) Se falhar (ex.: ins_documento), usa POST /documents/generate-sync (HTML local).
#
# A API tem de estar a correr (ex.: porta 3000). generate-sync exige Documents:AllowSyncPdfGeneration=true (Development).
#
# Uso:
#   Terminal 1: cd backend/KYX.DocEngine.API && ASPNETCORE_ENVIRONMENT=Development dotnet run
#   Terminal 2:
#     export BASE_URL=http://127.0.0.1:3000
#     export DOCENGINE_USERNAME=docengine.demo DOCENGINE_PASSWORD='DocEngine@2025'
#     bash docs/simplix-dossie/gerar-pdf-mock-api.sh
#
# Só generate-sync (sem slug na BD): export SKIP_TEMPLATE_API=1
#
# PDF: docs/preview/dossie-estrutura-mock-v2-api.pdf (ou dossie-estrutura-mock-api.pdf se SKIP_TEMPLATE_API=1)
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
: "${BASE_URL:=http://127.0.0.1:3000}"
export BASE_URL

# Último POST /documents/generate gravado pelo script (dados aninhados — ver mock oficial em mock-kit/)
REQ_POST="$ROOT/docs/preview/mock-kit/dossie-simplix-api-request.last-run.json"
# Contrato BD / payload aninhado (gerado junto; igual ao sample em docs/samples/)
REQ_BD="$ROOT/docs/preview/dossie-estrutura-generate-v2.request.json"
PDF="$ROOT/docs/preview/dossie-estrutura-mock-v2-api.pdf"

if [[ "${SKIP_TEMPLATE_API:-}" == "1" ]]; then
  node docs/scripts/post-dossie-estrutura-generate-sync.mjs \
    --request-out "$ROOT/docs/preview/dossie-estrutura-generate-sync.request.json" \
    --out-pdf "$ROOT/docs/preview/dossie-estrutura-mock-api.pdf"
  PDF="$ROOT/docs/preview/dossie-estrutura-mock-api.pdf"
else
  node docs/scripts/post-dossie-estrutura-generate-sync.mjs \
    --template-slug dossie-simplix-v2 \
    --request-out "$REQ_POST" \
    --estrutura-out "$REQ_BD" \
    --out-pdf "$PDF"
fi

echo "PDF gerado: $PDF ($(wc -c < "$PDF" | tr -d ' ') bytes)"
if [[ "$(uname -s)" == "Darwin" ]] && command -v open >/dev/null 2>&1; then
  open "$PDF" 2>/dev/null || true
fi
