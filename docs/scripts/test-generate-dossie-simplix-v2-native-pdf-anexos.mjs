#!/usr/bin/env node
/**
 * Testa POST /documents/generate com template dossie-simplix-v2 e anexosPdf
 * preenchidos com PDFs nativos (bytes em base64) a partir de ficheiros no repo.
 *
 * Por defeito usa os PDFs minúsculos em docs/preview/mock-kit/ (termo + proposta).
 * Caminhos extra: passar como argumentos (relativos à raiz do repo) ou EXTRA_PDF_REL.
 *
 *   DOCENGINE_USERNAME=docengine.demo DOCENGINE_PASSWORD='…' \
 *     node docs/scripts/test-generate-dossie-simplix-v2-native-pdf-anexos.mjs
 *
 *   node …/test-generate-dossie-simplix-v2-native-pdf-anexos.mjs book-admissional-fidelizza-campos.pdf
 *
 * Só gravar pedido (sem POST): SKIP_GENERATE=1
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
  'dossie-simplix-v2-native-pdfs-test.request.json'
);
const PDF_OUT = path.join(
  root,
  'docs',
  'preview',
  'mock-kit',
  'dossie-simplix-v2-native-pdfs-test.pdf'
);

const DEFAULT_RELS = [
  { rel: 'docs/preview/mock-kit/anexo-termo-adesao-mock.pdf', rotulo: 'Termo adesão (mock-kit)' },
  { rel: 'docs/preview/mock-kit/anexo-proposta-mock.pdf', rotulo: 'Proposta (mock-kit)' }
];

function sha256Buffer(buf) {
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
    throw new Error(`generate: ${res.status} ${JSON.stringify(json).slice(0, 4000)}`);
  }
  return json.resultado?.base64;
}

function resolvePdfSpecs() {
  const argv = process.argv.slice(2).filter(Boolean);
  if (argv.length) {
    return argv.map((rel, i) => ({
      rel,
      rotulo: path.basename(rel)
    }));
  }
  const extra = process.env.EXTRA_PDF_REL?.trim();
  const base = [...DEFAULT_RELS];
  if (extra) base.push({ rel: extra, rotulo: path.basename(extra) });
  return base;
}

function buildAnexosFromFiles(specs) {
  const ordemBase = Number(process.env.ANEXOS_ORDEM_BASE || '5');
  let ordem = ordemBase;
  const anexosPdf = [];
  for (const s of specs) {
    const abs = path.isAbsolute(s.rel) ? s.rel : path.join(root, s.rel);
    if (!fs.existsSync(abs)) throw new Error(`PDF em falta: ${s.rel}`);
    const buf = fs.readFileSync(abs);
    const head = buf.slice(0, 5).toString('latin1');
    if (head !== '%PDF-') {
      throw new Error(`Não é PDF (%PDF-): ${s.rel}`);
    }
    anexosPdf.push({
      ordem: ordem++,
      rotulo: s.rotulo,
      hashSha256: sha256Buffer(buf),
      base64: buf.toString('base64')
    });
  }
  return anexosPdf;
}

async function main() {
  const skipGenerate = process.env.SKIP_GENERATE === '1';
  const specs = resolvePdfSpecs();
  const anexosPdf = buildAnexosFromFiles(specs);

  const envelope = JSON.parse(fs.readFileSync(MOCK_IN, 'utf8'));
  envelope.requisicaoId = randomUUID();
  envelope.config = envelope.config || {};
  envelope.config.template = 'dossie-simplix-v2';
  envelope.config.centroCusto = envelope.config.centroCusto || 'DEMO';
  envelope.config.nomeArquivo =
    process.env.NOME_ARQUIVO_PDF || 'dossie-simplix-v2-native-pdfs-test.pdf';

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
  console.error('Anexos PDF nativos:', anexosPdf.length, specs.map((s) => s.rel).join(', '));

  if (skipGenerate) {
    console.log(JSON.stringify({ requestPath: REQ_OUT, annexCount: anexosPdf.length }));
    return;
  }

  const token = await login();
  console.error('POST /documents/generate …');
  const pdfB64 = await postGenerate(token, envelope);
  if (!pdfB64) throw new Error('Resposta sem base64.');
  fs.writeFileSync(PDF_OUT, Buffer.from(pdfB64, 'base64'));
  console.error('PDF:', PDF_OUT, 'bytes:', fs.statSync(PDF_OUT).size);
  console.log(
    JSON.stringify({
      requestPath: REQ_OUT,
      pdfPath: PDF_OUT,
      annexCount: anexosPdf.length,
      bytes: fs.statSync(PDF_OUT).size
    })
  );
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
