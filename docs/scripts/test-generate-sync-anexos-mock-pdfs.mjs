#!/usr/bin/env node
/**
 * Testa POST /documents/generate-sync com PDFs reais do repo (mock-kit).
 *
 * Pré-requisitos: API em Development com Documents:AllowSyncPdfGeneration=true.
 *
 * Uso:
 *   BASE_URL=http://127.0.0.1:3000 DOCENGINE_USERNAME=docengine.demo DOCENGINE_PASSWORD='DocEngine@2025' \
 *     node docs/scripts/test-generate-sync-anexos-mock-pdfs.mjs
 *
 * Opção: ANEXO_EXTRA=book  — inclui também book-admissional-fidelizza-campos.pdf (raiz do repo).
 *
 * Saída: OUT_PDF=caminho.pdf (opcional) sobrepõe o ficheiro gerado.
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { randomUUID } from 'crypto';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(__dirname, '..', '..');

const baseUrl = (process.env.BASE_URL || 'http://127.0.0.1:3000').replace(/\/$/, '');
const outPdf = process.env.OUT_PDF?.trim()
  ? path.resolve(process.cwd(), process.env.OUT_PDF.trim())
  : path.join(root, 'docs', 'preview', 'mock-kit', 'dossie-test-anexos-mock-kit.pdf');

const mockKit = path.join(root, 'docs', 'preview', 'mock-kit');
const anexosDefault = [
  path.join(mockKit, 'anexo-termo-adesao-mock.pdf'),
  path.join(mockKit, 'anexo-proposta-mock.pdf')
];

function b64File(p) {
  return fs.readFileSync(p).toString('base64');
}

async function login() {
  const u = process.env.DOCENGINE_USERNAME;
  const p = process.env.DOCENGINE_PASSWORD;
  if (!u || !p) {
    throw new Error('Defina DOCENGINE_USERNAME e DOCENGINE_PASSWORD.');
  }
  const r = await fetch(`${baseUrl}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ username: u, password: p })
  });
  const j = await r.json();
  if (!r.ok || !j.sucesso) {
    throw new Error(`Login falhou: ${r.status} ${JSON.stringify(j)}`);
  }
  return j.resultado.access_token;
}

const htmlMinimal = `<!DOCTYPE html>
<html lang="pt-BR">
<head><meta charset="utf-8" />
<style>
  body { font-family: Arial, sans-serif; padding: 24px; font-size: 14px; }
  h1 { font-size: 18px; }
</style>
</head>
<body>
  <h1>Teste DocEngine — corpo HTML mínimo</h1>
  <p>Seguem-se anexos PDF do mock-kit (termo + proposta). Rodapé: HASH_DOSSIE no Chrome; anexos carimbados no servidor.</p>
</body>
</html>`;

async function main() {
  const paths = [...anexosDefault];
  if (String(process.env.ANEXO_EXTRA || '').toLowerCase() === 'book') {
    paths.push(path.join(root, 'book-admissional-fidelizza-campos.pdf'));
  }

  for (const p of paths) {
    if (!fs.existsSync(p)) {
      throw new Error(`Ficheiro em falta: ${p}`);
    }
  }

  const pdfsAnexos = paths.map((file, index) => ({
    ordem: index + 1,
    base64: b64File(file)
  }));

  const hash = '0d7262794e2af4880398432552e94f098d0c2bfff17342eadfa6ea01294c3d15';

  const body = {
    requisicaoId: randomUUID(),
    nomeArquivo: 'dossie-test-anexos-mock-kit.pdf',
    inlineTemplate: { type: 'html', content: htmlMinimal, requiredFields: [] },
    dados: {
      HASH_DOSSIE: hash,
      DOCENGINE_USE_CHROME_PAGE_FOOTER: 'true'
    },
    pdfsAnexos
  };

  const token = process.env.TOKEN?.trim() || (await login());
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
    console.error(JSON.stringify(json, null, 2));
    process.exit(1);
  }
  const b64 = json.resultado?.base64;
  if (!b64) {
    console.error('Sem resultado.base64');
    process.exit(1);
  }
  fs.mkdirSync(path.dirname(outPdf), { recursive: true });
  fs.writeFileSync(outPdf, Buffer.from(b64, 'base64'));
  console.log('PDF gerado:', outPdf);
  console.log('Bytes:', fs.statSync(outPdf).size);
  console.log('Anexos:', paths.map((p) => path.basename(p)).join(', '));
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
