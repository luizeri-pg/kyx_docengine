#!/usr/bin/env node
/**
 * Gera SQL para gravar o template dossiê Simplix via psql (sem passar pela API).
 *
 * A DocEngine lista/lê templates com Dapper em tb_template:
 *   id_template, str_enum, str_descricao, str_type, str_content, campos (text[] no exemplo abaixo).
 * O teu ambiente pode ter tipos diferentes em `campos` — ajusta o cast se necessário.
 *
 * Uso:
 *   node docs/scripts/emit-dossie-template-sql.mjs --target partner \\
 *     | psql "$DATABASE_URL" -v ON_ERROR_STOP=1
 *
 * Base local (tabela EF `templates` das migrações — só útil se a API ler desta tabela):
 *   node docs/scripts/emit-dossie-template-sql.mjs --target ef | psql "$DATABASE_URL" -v ON_ERROR_STOP=1
 *
 * Fonte dos dados (slug, name, content, requiredFields):
 *   docs/preview/template-dossie-simplix-upsert.json (ou --json caminho)
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

function parseArgs(argv) {
  let target = 'partner';
  let jsonPath = path.join(__dirname, '../preview/template-dossie-simplix-upsert.json');
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--target' && argv[i + 1]) target = String(argv[++i]).toLowerCase();
    else if (a === '--json' && argv[i + 1]) jsonPath = path.resolve(process.cwd(), argv[++i]);
  }
  return { target, jsonPath };
}

/** Dollar-quote: escolhe tag que não apareça no texto. */
function dollarQuote(body, baseTag = 'dossie_html') {
  let tag = baseTag;
  let q = `$${tag}$`;
  while (body.includes(q)) {
    tag += '_x';
    q = `$${tag}$`;
  }
  return `${q}${body}${q}`;
}

function sqlArrayFromStrings(keys) {
  const parts = keys.map((k) => `'${String(k).replace(/'/g, "''")}'`);
  return `ARRAY[${parts.join(', ')}]::text[]`;
}

function emitPartnerSql(payload) {
  const { slug, name, type, content, requiredFields } = payload;
  const campos = sqlArrayFromStrings(requiredFields);
  const dqContent = dollarQuote(content, 'dossie_body');
  const header = `-- Template PDF Simplix → tb_template (schema esperado pela TemplateService / Dapper)
-- Colunas: str_enum, str_descricao, str_type, str_content, campos
-- Se tiveres id_template SERIAL, remove-o da lista de colunas abaixo ou usa DEFAULT.
-- Ajusta nomes de colunas / tipo de campos se o teu PostgreSQL plataforma for diferente.

BEGIN;

DELETE FROM tb_template WHERE str_enum = '${String(slug).replace(/'/g, "''")}';

INSERT INTO tb_template (str_enum, str_descricao, str_type, str_content, campos)
VALUES (
  '${String(slug).replace(/'/g, "''")}',
  '${String(name).replace(/'/g, "''")}',
  '${String(type).replace(/'/g, "''")}',
  ${dqContent},
  ${campos}
);

COMMIT;
`;
  return header;
}

function emitEfSql(payload) {
  const { slug, name, type, content, requiredFields } = payload;
  const rf = JSON.stringify(requiredFields).replace(/'/g, "''");
  const dqContent = dollarQuote(content, 'dossie_body_ef');
  return `-- Tabela EF "templates" (migração InitialCreate + snake_case)
-- Só serve se a aplicação ler desta tabela; o código actual usa tb_template + ins_tb_template na maior parte dos ambientes.

BEGIN;

INSERT INTO templates (id, slug, name, type, content, required_fields, is_active, created_at, updated_at)
VALUES (
  gen_random_uuid(),
  '${String(slug).replace(/'/g, "''")}',
  '${String(name).replace(/'/g, "''")}',
  '${String(type).replace(/'/g, "''")}',
  ${dqContent},
  '${rf}',
  TRUE,
  timezone('utc', now()),
  timezone('utc', now())
)
ON CONFLICT (slug) DO UPDATE SET
  name = EXCLUDED.name,
  type = EXCLUDED.type,
  content = EXCLUDED.content,
  required_fields = EXCLUDED.required_fields,
  updated_at = EXCLUDED.updated_at;

COMMIT;
`;
}

const { target, jsonPath } = parseArgs(process.argv);
const raw = fs.readFileSync(jsonPath, 'utf8');
const payload = JSON.parse(raw);
if (!payload.slug || !payload.content || !Array.isArray(payload.requiredFields)) {
  console.error('JSON inválido: precisa de slug, content, requiredFields[]');
  process.exit(1);
}

if (target === 'ef') {
  process.stdout.write(emitEfSql(payload));
} else {
  process.stdout.write(emitPartnerSql(payload));
}
