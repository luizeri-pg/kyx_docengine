#!/usr/bin/env node
/**
 * Dossiê principal: slug dossie-simplix-v2 (HTML template-dossie-simplix.html).
 * anexosPdf: cada entrada é um PDF gerado a partir de outro template HTML (Fidelizza, books, etc.).
 *
 * Fluxo:
 *   1) Lê docs/preview/mock-kit/dossie-simplix-api-request.mock.json
 *   2) Para cada HTML de anexo, POST /documents/generate-sync → base64 PDF
 *   3) Substitui dados.anexosPdf por esses PDFs (com hashSha256)
 *   4) Grava o pedido JSON e (opcional) POST /documents/generate
 *
 * Pré-requisitos: API em Development, DevFileTemplateFallback=true, SkipPartnerDocumentoPersist opcional,
 * Documents:AllowSyncPdfGeneration=true (para o passo sync dos anexos).
 *
 * Uso:
 *   DOCENGINE_USERNAME=docengine.demo DOCENGINE_PASSWORD='…' \
 *     node docs/scripts/generate-dossie-simplix-v2-anexos-from-html-templates.mjs
 *
 * Só gravar JSON sem POST final: SKIP_FINAL_GENERATE=1
 * Excluir o HTML do próprio dossiê dos anexos (evita duplicar o corpo): SKIP_SIMPLIX_HTML_ANNEX=1
 */
import fs from 'fs';
import path from 'path';
import { createHash, randomUUID } from 'crypto';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(__dirname, '..', '..');
const baseUrl = (process.env.BASE_URL || 'http://127.0.0.1:3000').replace(/\/$/, '');

const MOCK_IN = path.join(root, 'docs', 'preview', 'mock-kit', 'dossie-simplix-api-request.mock.json');
const REQ_OUT = path.join(
  root,
  'docs',
  'preview',
  'mock-kit',
  'dossie-simplix-v2-anexos-html-templates.request.json'
);
const PDF_OUT = path.join(
  root,
  'docs',
  'preview',
  'mock-kit',
  'dossie-simplix-v2-anexos-html-templates.pdf'
);

const HASH =
  process.env.HASH_DOSSIE_TEST ||
  '0d7262794e2af4880398432552e94f098d0c2bfff17342eadfa6ea01294c3d15';

const PNG_1X1 =
  'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';

/** HTMLs que passam a ser PDFs em anexosPdf (ordem = índice + ordemBase). */
const ANNEX_HTML_TEMPLATES = [
  { rotulo: 'Ficha Fidelizza (HTML→PDF)', rel: 'docs/templates/template-fidelizza.html' },
  { rotulo: 'Book admissional fidelidade', rel: 'docs/templates/template-book-admissional-fidelidade.html' },
  { rotulo: 'Book admissional Fidelizza 2025', rel: 'docs/templates/template-book-admissional-fidelizza-2025.html' },
  { rotulo: 'Book overlay', rel: 'docs/templates/template-book-admissional-fidelizza-overlay.html' },
  { rotulo: 'Dossiê Fidelizza zero (anexo)', rel: 'docs/templates/template-dossie-fidelizza-zero.html' },
  { rotulo: 'Dossiê Simplix (anexo HTML)', rel: 'docs/templates/template-dossie-simplix.html' }
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
    if (ku.includes('VT_LINHAS')) return '<tr><td>Linha 1</td><td>R$ 0,00</td></tr>';
    if (ku.includes('ANEXOS')) return '<p>Anexos (demonstração).</p>';
    return '<p><!-- ' + k + ' --></p>';
  }
  if (ku === 'TEXTO_JURIDICO') {
    return 'Texto jurídico de demonstração (anexo gerado a partir de HTML).';
  }
  if (
    /^(doc|raca|adiantamento|genero|orient|DOC_|RACA|VT_|ADIANTAMENTO|GENERO|ORIENT)/i.test(k) &&
    k.length < 52
  ) {
    return '[ ]';
  }
  if (/^orientOutrosTexto$/i.test(k)) return '—';
  if (/textoValeTransporte/i.test(k)) return 'Texto padrão de vale-transporte (demonstração).';
  return `«${k}»`;
}

/**
 * @param {{ chromeFooter?: boolean }} opts
 *   chromeFooter=false para PDFs que vão só como anexos fundidos: o rodapé global
 *   (hash + página/total) é aplicado depois por PdfDossieAnnexFooterStamper; se
 *   gerarmos aqui com DOCENGINE_USE_CHROME_PAGE_FOOTER=true, o hash fica duplicado
 *   por cima do carimbo.
 */
function buildDadosForHtml(html, opts = {}) {
  const chromeFooter = opts.chromeFooter !== false;
  const keys = extractPlaceholderKeys(html);
  const dados = {
    HASH_DOSSIE: HASH,
    DOCENGINE_USE_CHROME_PAGE_FOOTER: chromeFooter ? 'true' : 'false'
  };
  for (const k of keys) {
    if (k === 'HASH_DOSSIE' || k === 'DOCENGINE_USE_CHROME_PAGE_FOOTER') continue;
    dados[k] = defaultForKey(k);
  }
  return dados;
}

function sha256OfPdfBase64(b64) {
  const buf = Buffer.from(b64, 'base64');
  return createHash('sha256').update(buf).digest('hex');
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

async function postGenerateSync(token, body) {
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
    throw new Error(`generate-sync: ${res.status} ${JSON.stringify(json).slice(0, 2800)}`);
  }
  return json.resultado.base64;
}

async function htmlTemplateToPdfBase64(token, rel) {
  const abs = path.join(root, rel);
  if (!fs.existsSync(abs)) throw new Error(`Template em falta: ${rel}`);
  const html = fs.readFileSync(abs, 'utf8');
  const dados = buildDadosForHtml(html, { chromeFooter: false });
  const body = {
    requisicaoId: randomUUID(),
    nomeArquivo: path.basename(rel).replace(/\.html?$/i, '.pdf'),
    inlineTemplate: { type: 'html', content: html, requiredFields: [] },
    dados
  };
  return postGenerateSync(token, body);
}

async function postGenerate(token, body) {
  const res = await fetch(`${baseUrl}/documents/generate`, {
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
    throw new Error(`generate: ${res.status} ${JSON.stringify(json).slice(0, 3500)}`);
  }
  return json.resultado?.base64;
}

async function main() {
  const skipSimplixAnnex = process.env.SKIP_SIMPLIX_HTML_ANNEX === '1';
  const skipFinal = process.env.SKIP_FINAL_GENERATE === '1';

  const envelope = JSON.parse(fs.readFileSync(MOCK_IN, 'utf8'));
  envelope.requisicaoId = randomUUID();
  envelope.config = envelope.config || {};
  envelope.config.template = 'dossie-simplix-v2';
  envelope.config.nomeArquivo =
    process.env.NOME_ARQUIVO_PDF || 'dossie-simplix-v2-anexos-html-templates.pdf';
  envelope.config.centroCusto = envelope.config.centroCusto || 'DEMO';

  const token = await login();
  const ordemBase = Number(process.env.ANEXOS_ORDEM_BASE || '5');
  const anexosPdf = [];
  let ordem = ordemBase;

  for (const spec of ANNEX_HTML_TEMPLATES) {
    if (spec.rel.includes('template-dossie-simplix.html') && skipSimplixAnnex) continue;

    console.error('Gerando anexo PDF a partir de', spec.rel, '…');
    const b64 = await htmlTemplateToPdfBase64(token, spec.rel);
    anexosPdf.push({
      ordem: ordem++,
      rotulo: spec.rotulo,
      hashSha256: sha256OfPdfBase64(b64),
      base64: b64
    });
  }

  envelope.dados = envelope.dados || {};
  envelope.dados.anexosPdf = anexosPdf;
  envelope.dados.hashDossie =
    envelope.dados.hashDossie || 'f0e1d2c3b4a59687766554433221100fedcba9876543210fedcba9876543210';
  envelope.dados.docengineUseChromePageFooter = String(
    envelope.dados.docengineUseChromePageFooter ?? 'true'
  );

  fs.mkdirSync(path.dirname(REQ_OUT), { recursive: true });
  fs.writeFileSync(REQ_OUT, JSON.stringify(envelope, null, 2) + '\n', 'utf8');
  console.error('Pedido gravado:', REQ_OUT);
  console.error('Anexos (HTML→PDF):', anexosPdf.length);

  if (skipFinal) {
    console.log(JSON.stringify({ requestPath: REQ_OUT, annexCount: anexosPdf.length }));
    return;
  }

  console.error('POST /documents/generate …');
  const pdfB64 = await postGenerate(token, envelope);
  if (!pdfB64) throw new Error('Resposta sem base64.');
  fs.writeFileSync(PDF_OUT, Buffer.from(pdfB64, 'base64'));
  console.error('PDF:', PDF_OUT, 'bytes:', fs.statSync(PDF_OUT).size);
  console.log(JSON.stringify({ requestPath: REQ_OUT, pdfPath: PDF_OUT, annexCount: anexosPdf.length }));
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
