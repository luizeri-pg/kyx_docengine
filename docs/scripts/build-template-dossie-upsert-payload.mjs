#!/usr/bin/env node
/**
 * Gera o corpo JSON para POST /templates (UpsertTemplateRequest) a partir do HTML do dossiê Simplix.
 *
 * Uso:
 *   node docs/scripts/build-template-dossie-upsert-payload.mjs
 *     → grava docs/preview/template-dossie-simplix-upsert.json (slug dossie-simplix)
 *
 *   node docs/scripts/build-template-dossie-upsert-payload.mjs --stdout
 *     → imprime JSON no stdout (útil para pipe em curl)
 *
 * Variante pasta docs/simplix-dossie/ (mesmo HTML, outro slug na API):
 *   node docs/scripts/build-template-dossie-upsert-payload.mjs \\
 *     --slug simplix-dossie \\
 *     --name "Dossiê probatório – Simplix" \\
 *     --out docs/simplix-dossie/template-upsert.json
 *
 * Novo slug sem conflito com dossie-simplix já na BD (v2, mesmo HTML):
 *   node docs/scripts/build-template-dossie-upsert-payload.mjs \\
 *     --slug dossie-simplix-v2 \\
 *     --name "Dossiê probatório – Contratação Digital Simplix (v2)" \\
 *     --out docs/preview/template-dossie-simplix-v2-upsert.json
 *
 * Opções: --slug, --name, --html <ficheiro>, --out <ficheiro.json>
 *
 * A rota exige autenticação ([Authorize]); envie o Bearer token no header.
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

function parseArgs(argv) {
  const o = {
    slug: 'dossie-simplix',
    name: 'Dossiê probatório – Contratação Digital Simplix',
    htmlPath: path.join(__dirname, '../templates/template-dossie-simplix.html'),
    outPath: path.join(__dirname, '../preview/template-dossie-simplix-upsert.json'),
    stdout: false
  };
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--stdout') {
      o.stdout = true;
      continue;
    }
    if ((a === '--slug' || a === '--name' || a === '--html' || a === '--out') && argv[i + 1]) {
      const v = argv[++i];
      if (a === '--slug') o.slug = v;
      else if (a === '--name') o.name = v;
      else if (a === '--html') o.htmlPath = path.resolve(process.cwd(), v);
      else if (a === '--out') o.outPath = path.resolve(process.cwd(), v);
    }
  }
  return o;
}

function buildPayload(content, slug, name) {
  /** Campos usados no HTML {{CHAVE}}. */
  const PLACEHOLDER_KEYS = [
    ...new Set([...content.matchAll(/\{\{\s*([A-Za-z0-9_]+)\s*\}\}/g)].map((m) => m[1]))
  ].sort();

  /** Não exigir na API: placeholders opcionais removidos do HTML ou preenchidos vazios. */
  const OPTIONAL_PLACEHOLDER_KEYS = new Set([]);

  /** Chaves enviadas em `dados` mas não aparecem como {{}} no HTML (motor PDF / rodapé Chromium). */
  const EXTRA_REQUIRED_DATOS_KEYS = ['HASH_DOSSIE', 'DOCENGINE_USE_CHROME_PAGE_FOOTER'];

  const requiredFields = [
    ...new Set([
      ...PLACEHOLDER_KEYS.filter((k) => !OPTIONAL_PLACEHOLDER_KEYS.has(k)),
      ...EXTRA_REQUIRED_DATOS_KEYS
    ])
  ].sort();

  return {
    payload: {
      slug,
      name,
      type: 'html',
      content,
      /**
       * Validação na geração: todos os placeholders do HTML (exceto DOSSIE_BLOCO_INTERCALADO_HTML) + HASH_DOSSIE + DOCENGINE_USE_CHROME_PAGE_FOOTER.
       * Payload estruturado da equipa: converter com docs/scripts/estrutura-dossie-to-flat-dados.mjs e juntar CCB/termos/logo (ex.: docs/templates/dossie-simplix-template-dados.sem-imagens.json).
       */
      requiredFields
    },
    PLACEHOLDER_KEYS,
    requiredFields
  };
}

const opts = parseArgs(process.argv);
const content = fs.readFileSync(opts.htmlPath, 'utf8');
const { payload, PLACEHOLDER_KEYS, requiredFields } = buildPayload(content, opts.slug, opts.name);
const json = JSON.stringify(payload);

if (opts.stdout) {
  process.stdout.write(json);
} else {
  fs.mkdirSync(path.dirname(opts.outPath), { recursive: true });
  fs.writeFileSync(opts.outPath, json, 'utf8');
  console.log('Arquivo:', opts.outPath);
  console.log('Slug:', opts.slug);
  console.log('HTML:', opts.htmlPath);
  console.log('Tamanho do content (chars):', content.length);
  console.log('Placeholders únicos no HTML:', PLACEHOLDER_KEYS.length);
  console.log('requiredFields (gravados no template):', requiredFields.length);
}
