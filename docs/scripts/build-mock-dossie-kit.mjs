#!/usr/bin/env node
/**
 * Gera um kit de demonstração em docs/preview/mock-kit/:
 * - PDFs anexo mínimos (válidos) para config.pdfsAnexosBase64
 * - dossie-simplix-api-request.flat.mock.json — POST exemplo com dados já planos + pdfsAnexosBase64
 *   (o contrato aninhado oficial está em dossie-simplix-api-request.mock.json — não sobrescrito aqui)
 * - Tenta regenerar dossie-simplix-mock.pdf (Chrome headless) e copia para mock-kit
 *
 * Uso: node docs/scripts/build-mock-dossie-kit.mjs
 */
import { readFileSync, writeFileSync, mkdirSync, existsSync, copyFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { execSync } from 'child_process';
import { createMinimalOnePagePdf } from './minimal-pdf.mjs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const docsDir = join(__dirname, '..');
const mockKit = join(docsDir, 'preview', 'mock-kit');
const semImagensPath = join(docsDir, 'templates', 'dossie-simplix-template-dados.sem-imagens.json');
const mockPdfSrc = join(docsDir, 'preview', 'dossie-simplix-mock.pdf');
const mockPdfDest = join(mockKit, 'dossie-simplix-mock.pdf');

mkdirSync(mockKit, { recursive: true });

execSync('node docs/scripts/build-dossie-simplix-template-dados.mjs --no-images', {
  cwd: join(__dirname, '..', '..'),
  stdio: 'inherit'
});

const envelope = JSON.parse(readFileSync(semImagensPath, 'utf-8'));
const { dados } = envelope;

const propostaBuf = createMinimalOnePagePdf('Proposta comercial - PDF mock (KYX DocEngine)');
const termoBuf = createMinimalOnePagePdf('Termo de adesao - PDF mock (KYX DocEngine)');
const propostaPath = join(mockKit, 'anexo-proposta-mock.pdf');
const termoPath = join(mockKit, 'anexo-termo-adesao-mock.pdf');
writeFileSync(propostaPath, propostaBuf);
writeFileSync(termoPath, termoBuf);

const request = {
  requisicaoId: 'a1b2c3d4-e5f6-4789-a012-3456789abcde',
  config: {
    template: 'dossie-simplix',
    centroCusto: 'MOCK_DOCENGINE',
    nomeArquivo: 'dossie-simplix-mock-com-anexos.pdf',
    pdfsAnexosBase64: [propostaBuf.toString('base64'), termoBuf.toString('base64')]
  },
  dados
};

const jsonPathFlat = join(mockKit, 'dossie-simplix-api-request.flat.mock.json');
writeFileSync(jsonPathFlat, JSON.stringify(request, null, 2), 'utf-8');

console.log('Kit mock-kit:');
console.log('  ', propostaPath);
console.log('  ', termoPath);
console.log('  ', jsonPathFlat, '(dados planos; contrato aninhado: dossie-simplix-api-request.mock.json)');

const chromeMac =
  '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
const htmlResolved = join(docsDir, 'preview', 'dossie-simplix-resolved.html');

function tryCopyDossieMockPdf() {
  if (existsSync(mockPdfSrc)) {
    copyFileSync(mockPdfSrc, mockPdfDest);
    console.log('  ', mockPdfDest, '(copiado de docs/preview/)');
    return true;
  }
  return false;
}

if (existsSync(chromeMac)) {
  try {
    execSync('node docs/scripts/generate-mock-pdf.mjs', {
      cwd: join(__dirname, '..', '..'),
      stdio: 'inherit'
    });
  } catch (e) {
    console.warn('generate-mock-pdf falhou (sandbox/Chrome):', e.message);
  }
  if (!tryCopyDossieMockPdf()) {
    console.warn(
      '  Sem dossie-simplix-mock.pdf em docs/preview/. Rode fora do sandbox: node docs/scripts/generate-mock-pdf.mjs'
    );
  }
} else {
  console.warn('Chrome nao encontrado em', chromeMac, '- pulei generate-mock-pdf.mjs.');
  if (!tryCopyDossieMockPdf()) {
    console.warn('  Nenhum PDF dossiê em docs/preview/ para copiar.');
  }
}

const dadosComImagensPath = join(docsDir, 'preview', 'dossie-simplix-template-dados.json');
const fullRequestPath = join(mockKit, 'dossie-simplix-api-request.full.json');
if (existsSync(dadosComImagensPath)) {
  const env = JSON.parse(readFileSync(dadosComImagensPath, 'utf-8'));
  const fullReq = {
    requisicaoId: request.requisicaoId,
    config: {
      ...request.config,
      pdfsAnexosBase64: request.config.pdfsAnexosBase64
    },
    dados: env.dados
  };
  writeFileSync(fullRequestPath, JSON.stringify(fullReq), 'utf-8');
  console.log('\nJSON API completo (dados + imagens + anexos):', fullRequestPath);
  console.log('  Tamanho (bytes):', Buffer.byteLength(JSON.stringify(fullReq), 'utf8'));
}

console.log('\nPOST /documents/generate — leve plano (sem imagens):', jsonPathFlat);
console.log('POST /documents/generate — aninhado (Simplix):', join(mockKit, 'dossie-simplix-api-request.mock.json'));
