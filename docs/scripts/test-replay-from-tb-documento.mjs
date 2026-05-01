#!/usr/bin/env node
/**
 * Reaplica um payload retirado de tb_documento.dados (snapshot do partner DB)
 * contra POST /documents/generate, usando o slug certo do template.
 *
 *   DOCENGINE_USERNAME=docengine.demo DOCENGINE_PASSWORD='…' \
 *     node docs/scripts/test-replay-from-tb-documento.mjs \
 *       docs/preview/dossie-replay/id-928-dados.json dossie-simplix-v2
 *
 * Saída ao lado do JSON: <basename>.replay.pdf.
 *
 * Mantém intactos:
 *   - hashDossie / docengineUseChromePageFooter (e seus uppercase)
 *   - anexosPdf[] / pdfsAnexos no payload (estruturado ou achatado)
 */
import fs from 'fs';
import path from 'path';
import { randomUUID } from 'crypto';
import { fileURLToPath } from 'url';

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

async function main() {
  const [dadosPath, slug = 'dossie-simplix-v2', nomeArquivo] = process.argv.slice(2);
  if (!dadosPath) {
    console.error('Uso: node test-replay-from-tb-documento.mjs <dados.json> [slug] [nomeArquivo]');
    process.exit(2);
  }
  const dadosAbs = path.isAbsolute(dadosPath) ? dadosPath : path.join(root, dadosPath);
  const raw = fs.readFileSync(dadosAbs, 'utf8').trim();
  const dados = JSON.parse(raw);

  const envelope = {
    requisicaoId: randomUUID(),
    config: {
      template: slug,
      centroCusto: 'DEMO',
      nomeArquivo:
        nomeArquivo || path.basename(dadosAbs).replace(/\.json$/i, '.replay.pdf')
    },
    dados
  };

  const annexCount = Array.isArray(dados?.anexosPdf) ? dados.anexosPdf.length : 0;
  const pdfsAnexosCount = Array.isArray(dados?.pdfsAnexos) ? dados.pdfsAnexos.length : 0;
  console.error(
    `Reaplicando: slug=${slug}, dados.anexosPdf=${annexCount}, dados.pdfsAnexos=${pdfsAnexosCount}, hash=${
      dados?.hashDossie || dados?.HASH_DOSSIE || '<sem hash>'
    }, footer=${dados?.docengineUseChromePageFooter ?? dados?.DOCENGINE_USE_CHROME_PAGE_FOOTER ?? '<sem flag>'}`
  );

  const outPdf = path.join(
    path.dirname(dadosAbs),
    path.basename(dadosAbs).replace(/\.json$/i, '.replay.pdf')
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
  console.log(
    JSON.stringify({
      requestPath: dadosAbs,
      pdfPath: outPdf,
      bytes: fs.statSync(outPdf).size,
      slug,
      anexosPdf: annexCount,
      pdfsAnexos: pdfsAnexosCount
    })
  );
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
