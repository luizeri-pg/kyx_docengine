#!/usr/bin/env node
/**
 * Com a API em http://localhost:3000 (dotnet run em Development):
 * 1) Login docengine.demo / DocEngine@2025
 * 2) POST /documents/generate-sync com template-dossie-simplix.html + dados do JSON sem imagens
 * 3) Grava docs/preview/dossie-simplix-smoke-api.pdf
 *
 * Uso: node docs/scripts/smoke-dossie-generate-sync.mjs
 */
import { readFileSync, writeFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { randomUUID } from 'crypto';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..', '..');
const docs = join(root, 'docs');
const base = process.env.DOCENGINE_API_URL || 'http://localhost:3000';

const html = readFileSync(join(docs, 'templates', 'template-dossie-simplix.html'), 'utf-8');
const envelope = JSON.parse(
  readFileSync(join(docs, 'templates', 'dossie-simplix-template-dados.sem-imagens.json'), 'utf-8')
);
const { dados } = envelope;

const loginRes = await fetch(`${base}/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
  body: JSON.stringify({ username: 'docengine.demo', password: 'DocEngine@2025' })
});
const loginJson = await loginRes.json();
if (!loginRes.ok || !loginJson.sucesso) {
  console.error('Login falhou:', loginRes.status, loginJson);
  process.exit(1);
}
const token = loginJson.resultado?.access_token ?? loginJson.resultado?.accessToken;
if (!token) {
  console.error('Resposta de login sem access_token:', loginJson);
  process.exit(1);
}

const syncRes = await fetch(`${base}/documents/generate-sync`, {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    Accept: 'application/json',
    Authorization: `Bearer ${token}`
  },
  body: JSON.stringify({
    requisicaoId: randomUUID(),
    nomeArquivo: 'dossie-simplix-smoke-api.pdf',
    inlineTemplate: {
      type: 'html',
      content: html,
      requiredFields: []
    },
    dados
  })
});
const syncJson = await syncRes.json();
if (!syncRes.ok || !syncJson.sucesso) {
  console.error('generate-sync falhou:', syncRes.status, syncJson);
  process.exit(1);
}

const b64 = syncJson.resultado?.base64;
if (!b64) {
  console.error('Resposta sem base64:', syncJson);
  process.exit(1);
}

const out = join(docs, 'preview', 'dossie-simplix-smoke-api.pdf');
writeFileSync(out, Buffer.from(b64, 'base64'));
console.log('PDF gerado:', out);
console.log('Tempo ms:', syncJson.tempoProcessamento);
