#!/usr/bin/env node
/**
 * Gera PDFs via POST /documents/generate-sync para vários templates HTML,
 * com HASH_DOSSIE + DOCENGINE_USE_CHROME_PAGE_FOOTER e (opcional) anexos PDF
 * para validar rodapé Chromium + carimbo PdfSharp nos anexos.
 *
 * Pré-requisitos: API em Development, Documents:AllowSyncPdfGeneration=true,
 * BASE_URL, DOCENGINE_USERNAME, DOCENGINE_PASSWORD.
 *
 * Uso:
 *   node docs/scripts/batch-test-templates-hash-pagination.mjs
 *   SKIP_ANNEX=1 node …   — só corpo HTML, sem pdfsAnexos
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { randomUUID } from 'crypto';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(__dirname, '..', '..');
const baseUrl = (process.env.BASE_URL || 'http://127.0.0.1:3000').replace(/\/$/, '');
const outDir = path.join(root, 'docs', 'preview', 'template-hash-tests');
const skipAnnex = process.env.SKIP_ANNEX === '1';

const HASH =
  process.env.HASH_DOSSIE_TEST ||
  '0d7262794e2af4880398432552e94f098d0c2bfff17342eadfa6ea01294c3d15';

const PNG_1X1 =
  'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';

const TEMPLATES = [
  { slug: 'fidelizza', rel: 'docs/templates/template-fidelizza.html' },
  { slug: 'book-fidelidade', rel: 'docs/templates/template-book-admissional-fidelidade.html' },
  { slug: 'book-fidelizza-2025', rel: 'docs/templates/template-book-admissional-fidelizza-2025.html' },
  { slug: 'book-overlay', rel: 'docs/templates/template-book-admissional-fidelizza-overlay.html' },
  { slug: 'dossie-fidelizza-zero', rel: 'docs/templates/template-dossie-fidelizza-zero.html' },
  { slug: 'dossie-simplix', rel: 'docs/templates/template-dossie-simplix.html' }
];

function extractPlaceholderKeys(html) {
  const re = /\{\{\s*([^}]+?)\s*\}\}/g;
  const s = new Set();
  let m;
  while ((m = re.exec(html))) {
    const k = m[1].trim();
    if (k && !k.startsWith('!')) s.add(k);
  }
  return [...s];
}

function defaultForKey(key) {
  const k = key;
  const ku = k.toUpperCase();
  if (ku === 'HASH_DOSSIE') return HASH;
  if (ku === 'DOCENGINE_USE_CHROME_PAGE_FOOTER') return 'true';
  if (k.startsWith('IMG_') || k === 'LOGO' || k === 'LOGO_SIMPLIX_BASE64') return PNG_1X1;
  if (ku.endsWith('_HTML') || ku.includes('HTML')) {
    if (ku.includes('EVENTOS')) {
      return '<tr><td>Teste</td><td>16/04/2026 10:00</td><td>IP 127.0.0.1</td></tr>';
    }
    if (ku.includes('INTERAC')) {
      return '<tr><td>16/04/2026</td><td>Sistema</td><td>Mensagem de teste.</td></tr>';
    }
    if (ku.includes('VT_LINHAS')) {
      return '<tr><td>Linha 1</td><td>R$ 0,00</td></tr>';
    }
    if (ku.includes('ANEXOS')) return '<p>Anexos (teste).</p>';
    return '<p><!-- ' + k + ' --></p>';
  }
  if (ku === 'TEXTO_JURIDICO') {
    return 'Texto jurídico de demonstração para geração de PDF (hash e paginação em teste).';
  }
  if (
    /^(doc|raca|adiantamento|genero|orient|DOC_|RACA|VT_|ADIANTAMENTO|GENERO|ORIENT)/i.test(k) &&
    k.length < 52
  ) {
    return '[ ]';
  }
  if (/^orientOutrosTexto$/i.test(k)) return '—';
  if (/textoValeTransporte/i.test(k)) {
    return 'Texto padrão de vale-transporte (demonstração).';
  }
  return `«${k}»`;
}

function buildDados(keys) {
  const dados = {
    HASH_DOSSIE: HASH,
    DOCENGINE_USE_CHROME_PAGE_FOOTER: 'true'
  };
  for (const k of keys) {
    if (k === 'HASH_DOSSIE' || k === 'DOCENGINE_USE_CHROME_PAGE_FOOTER') continue;
    dados[k] = defaultForKey(k);
  }
  return dados;
}

function loadAnnexPdfs() {
  const mockKit = path.join(root, 'docs', 'preview', 'mock-kit');
  const files = [
    path.join(mockKit, 'anexo-termo-adesao-mock.pdf'),
    path.join(mockKit, 'anexo-proposta-mock.pdf')
  ];
  const pdfsAnexos = [];
  let i = 1;
  for (const f of files) {
    if (fs.existsSync(f)) {
      pdfsAnexos.push({ ordem: i++, base64: fs.readFileSync(f).toString('base64') });
    }
  }
  return pdfsAnexos;
}

async function login() {
  const u = process.env.DOCENGINE_USERNAME;
  const p = process.env.DOCENGINE_PASSWORD;
  if (!u || !p) throw new Error('Defina DOCENGINE_USERNAME e DOCENGINE_PASSWORD.');
  const r = await fetch(`${baseUrl}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ username: u, password: p })
  });
  const j = await r.json();
  if (!r.ok || !j.sucesso) throw new Error(`Login: ${r.status} ${JSON.stringify(j)}`);
  return j.resultado.access_token;
}

async function postSync(token, body) {
  const res = await fetch(`${baseUrl}/documents/generate-sync`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
      Authorization: `Bearer ${token}`
    },
    body: JSON.stringify(body)
  });
  const json = await res.json();
  if (!res.ok || !json.sucesso) {
    throw new Error(`${res.status} ${JSON.stringify(json).slice(0, 2500)}`);
  }
  return json.resultado.base64;
}

async function main() {
  fs.mkdirSync(outDir, { recursive: true });
  const token = process.env.TOKEN?.trim() || (await login());
  const annex = skipAnnex ? [] : loadAnnexPdfs();
  const report = [];

  for (const t of TEMPLATES) {
    const abs = path.join(root, t.rel);
    if (!fs.existsSync(abs)) {
      report.push({ slug: t.slug, ok: false, err: 'ficheiro inexistente' });
      continue;
    }
    const html = fs.readFileSync(abs, 'utf8');
    const keys = extractPlaceholderKeys(html);
    const dados = buildDados(keys);
    const body = {
      requisicaoId: randomUUID(),
      nomeArquivo: `${t.slug}-hash-test.pdf`,
      inlineTemplate: { type: 'html', content: html, requiredFields: [] },
      dados,
      ...(annex.length ? { pdfsAnexos: annex } : {})
    };
    try {
      const t0 = Date.now();
      const b64 = await postSync(token, body);
      const outPdf = path.join(outDir, `${t.slug}.pdf`);
      fs.writeFileSync(outPdf, Buffer.from(b64, 'base64'));
      report.push({
        slug: t.slug,
        ok: true,
        ms: Date.now() - t0,
        bytes: fs.statSync(outPdf).size,
        out: outPdf,
        placeholders: keys.length,
        annexPages: annex.length
      });
    } catch (e) {
      report.push({ slug: t.slug, ok: false, err: String(e.message || e) });
    }
  }

  fs.writeFileSync(path.join(outDir, 'report.json'), JSON.stringify(report, null, 2) + '\n', 'utf8');
  console.log(JSON.stringify(report, null, 2));
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
