#!/usr/bin/env bash
# Grava o template dossiê em tb_template (schema partner esperado pela API).
#
# Exporta PG* antes do pipe — senão o psql não herda PGPASSWORD.
# Exemplo (alinhado a appsettings.Local.json típico):
#   export PGHOST=172.19.61.24 PGPORT=5442 PGDATABASE=doc_engine_dev PGUSER=doc_engine
#   export PGPASSWORD='…'
#   bash docs/simplix-dossie/psql-seed-partner.sh
#
# Se aparecer "permission denied for table tb_template", o DBA tem de conceder
# INSERT/UPDATE/DELETE (ou usar POST /templates com utilizador que chame ins_tb_template).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
export PGHOST="${PGHOST:-localhost}"
export PGPORT="${PGPORT:-5432}"
export PGDATABASE="${PGDATABASE:-docengine}"
export PGUSER="${PGUSER:-postgres}"
export PGPASSWORD="${PGPASSWORD:-postgres}"

JSON="${DOSSIE_TEMPLATE_JSON:-$ROOT/docs/preview/template-dossie-simplix-upsert.json}"
node "$ROOT/docs/scripts/emit-dossie-template-sql.mjs" --json "$JSON" --target partner \
  | psql -v ON_ERROR_STOP=1
