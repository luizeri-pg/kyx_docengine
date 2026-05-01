#!/usr/bin/env node
/**
 * Reaplica um payload "produção-like" (achatado, anexos em config.pdfsAnexos)
 * contra POST /documents/generate e grava a resposta.
 *
 *   DOCENGINE_USERNAME=docengine.demo DOCENGINE_PASSWORD='…' \
 *     node docs/scripts/test-replay-prod-like-payload.mjs \
 *       docs/preview/dossie-estrutura-generate-v2.post-docengine.json \
 *       docs/preview/mock-kit/anexo-termo-adesao-mock.pdf \
 *       docs/preview/mock-kit/anexo-proposta-mock.pdf
 *
 * Argumento 1: ficheiro JSON do pedido. Argumentos seguintes (opcionais):
 * caminhos para PDFs reais que substituem os pdfsAnexos placeholder dentro do JSON
 * (mantendo a ordem do array).
 *
 * Saída: <basename>.replay.pdf no mesmo diretório do JSON.
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { randomUUID } from 'crypto';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(__dirname, '..', '..');
const baseUrl = (process.env.BASE_URL || 'http://127.0.0.1:3000').replace(/\/$/, '');

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

function readPdfBase64(rel) {
  const abs = path.isAbsolute(rel) ? rel : path.join(root, rel);
  const buf = fs.readFileSync(abs);
  if (!buf.slice(0, 5).toString('latin1').startsWith('%PDF-')) {
    throw new Error(`Não é PDF: ${rel}`);
  }
  return buf.toString('base64');
}

async function main() {
  const [requestPath, ...pdfRels] = process.argv.slice(2);
  if (!requestPath) {
    console.error('Uso: node test-replay-prod-like-payload.mjs <request.json> [pdf1 pdf2 ...]');
    process.exit(2);
  }
  const reqAbs = path.isAbsolute(requestPath) ? requestPath : path.join(root, requestPath);
  const envelope = JSON.parse(fs.readFileSync(reqAbs, 'utf8'));
  envelope.requisicaoId = randomUUID();

  if (pdfRels.length) {
    envelope.config = envelope.config || {};
    envelope.config.pdfsAnexos = envelope.config.pdfsAnexos || [];
    const replaced = [];
    let i = 0;
    for (; i < pdfRels.length; i++) {
      const ord = (envelope.config.pdfsAnexos[i]?.ordem ?? (i + 5));
      replaced.push({ ordem: ord, base64: readPdfBase64(pdfRels[i]) });
    }
    envelope.config.pdfsAnexos = replaced;
    delete envelope.config.pdfsAnexosBase64;
  }

  envelope.config = envelope.config || {};
  envelope.config.template = envelope.config.template || 'dossie-simplix-v2';
  envelope.config.centroCusto = envelope.config.centroCusto || 'DEMO';
  envelope.config.nomeArquivo = envelope.config.nomeArquivo || 'replay.pdf';

  const outPdf = path.join(
    path.dirname(reqAbs),
    path.basename(reqAbs).replace(/\.json$/i, '.replay.pdf')
  );

  const token = await login();
  const res = await fetch(`${baseUrl}/documents/generate`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
      Authorization: `Bearer ${token}`
    },
    body: JSON.stringify(envelope)
  });
  const json = await res.json();
  if (!res.ok || !json.sucesso) {
    throw new Error(`generate: ${res.status} ${JSON.stringify(json).slice(0, 4000)}`);
  }
  const b64 = json.resultado?.base64;
  if (!b64) throw new Error('Resposta sem base64.');
  fs.writeFileSync(outPdf, Buffer.from(b64, 'base64'));
  console.log(JSON.stringify({
    requestPath: reqAbs,
    pdfPath: outPdf,
    bytes: fs.statSync(outPdf).size,
    pdfsAnexos: envelope.config.pdfsAnexos?.length ?? 0,
    template: envelope.config.template
  }));
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
