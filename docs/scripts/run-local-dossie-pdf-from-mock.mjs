#!/usr/bin/env node
/**
 * Gera PDF localmente usando o payload estruturado (ex.: docs/samples/dossie-payload-mock.json):
 * mescla `buildTemplateDadosFromEstrutura(payload)` com o envelope completo do build (termos/CCB em HTML)
 * e anexa PDFs de `anexosPdf` via `pdfsAnexosBase64`.
 *
 * Uso:
 *   LOCAL_DOCENGINE_NO_IMAGES=1 node docs/scripts/run-local-dossie-pdf-from-mock.mjs
 *   DOSSIE_MOCK_JSON=docs/samples/dossie-payload-mock.json node docs/scripts/run-local-dossie-pdf-from-mock.mjs
 */
import { spawn, execSync } from 'child_process';
import { readFileSync, writeFileSync, existsSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { randomUUID } from 'crypto';
import {
  buildTemplateDadosFromEstrutura,
  buildPdfsAnexosTermosPoliticaECcb
} from './estrutura-dossie-to-flat-dados.mjs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..', '..');
const docs = join(root, 'docs');
const port = process.env.LOCAL_DOCENGINE_PORT || '5317';
const base = `http://127.0.0.1:${port}`;
const csproj = join(root, 'backend', 'KYX.DocEngine.API', 'KYX.DocEngine.API.csproj');
const requestJsonPath = join(docs, 'preview', 'dossie-simplix-generate-sync.from-mock.request.json');
const dadosSemImagensPath = join(docs, 'templates', 'dossie-simplix-template-dados.sem-imagens.json');
const mockPath = join(root, process.env.DOSSIE_MOCK_JSON || 'docs/samples/dossie-payload-mock.json');
const pdfOutPath = join(docs, 'preview', 'dossie-simplix-payload-mock.pdf');
const demoIntercaladoPdfPath = join(docs, 'fidelizza-2025-split', 'demo-generate-response.pdf');

const childEnv = {
  ...process.env,
  ASPNETCORE_ENVIRONMENT: 'Development',
  ASPNETCORE_URLS: `http://127.0.0.1:${port}`,
  Auth__Mode: 'FallbackOnly',
  Database__ApplyMigrationsOnStartup: 'false',
  Documents__AllowSyncPdfGeneration: 'true'
};

function waitForHttp(url, timeoutMs = 90000) {
  const deadline = Date.now() + timeoutMs;
  return new Promise((resolve, reject) => {
    const tryOnce = async () => {
      if (Date.now() > deadline) {
        reject(new Error(`Timeout à espera de ${url}`));
        return;
      }
      try {
        const r = await fetch(url);
        if (r.ok) {
          resolve();
          return;
        }
      } catch {
        /* ainda a subir */
      }
      setTimeout(tryOnce, 350);
    };
    tryOnce();
  });
}

function stripMetaKeys(o) {
  const { meta, _descricao, _ordemNoDossie, ...rest } = o;
  return rest;
}

async function generatePdfFromMock(token, baseEnvelopePath, payloadEstruturado) {
  const html = readFileSync(join(docs, 'templates', 'template-dossie-simplix.html'), 'utf-8');
  const envelope = JSON.parse(readFileSync(baseEnvelopePath, 'utf-8'));
  const flat = buildTemplateDadosFromEstrutura(payloadEstruturado);
  const dados = { ...envelope.dados, ...flat };
  const anexos = buildPdfsAnexosTermosPoliticaECcb(payloadEstruturado);

  const body = {
    requisicaoId: randomUUID(),
    nomeArquivo: 'dossie-simplix-payload-mock.pdf',
    inlineTemplate: { type: 'html', content: html, requiredFields: [] },
    dados
  };
  if (anexos.length > 0) {
    body.pdfsAnexosBase64 = anexos;
  }
  if (process.env.DOSSIE_USE_RASTER_INTERCALADO_ONLY !== '1' && existsSync(demoIntercaladoPdfPath)) {
    body.pdfIntercaladoBase64 = readFileSync(demoIntercaladoPdfPath).toString('base64');
    dados.DOSSIE_BLOCO_INTERCALADO_HTML = '';
  }

  writeFileSync(requestJsonPath, JSON.stringify(body), 'utf-8');

  const syncRes = await fetch(`${base}/documents/generate-sync`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
      Authorization: `Bearer ${token}`
    },
    body: JSON.stringify(body)
  });
  const syncJson = await syncRes.json();
  if (!syncRes.ok || !syncJson.sucesso) {
    throw new Error(`generate-sync: ${syncRes.status} ${JSON.stringify(syncJson)}`);
  }
  const b64 = syncJson.resultado?.base64;
  if (!b64) throw new Error('Resposta sem base64');
  writeFileSync(pdfOutPath, Buffer.from(b64, 'base64'));
  return { pdfPath: pdfOutPath, requestJsonPath };
}

const proc = spawn('dotnet', ['run', '--project', csproj, '--no-launch-profile', '-v', 'q'], {
  cwd: root,
  env: childEnv,
  stdio: ['ignore', 'pipe', 'pipe']
});

let bootLog = '';
const append = (buf) => {
  bootLog += buf.toString();
  if (bootLog.length > 12000) bootLog = bootLog.slice(-8000);
};

proc.stdout?.on('data', append);
proc.stderr?.on('data', append);

proc.on('error', (err) => {
  console.error('Falha ao iniciar dotnet:', err.message);
  process.exit(1);
});

const cleanup = () => {
  try {
    proc.kill('SIGTERM');
  } catch {
    /* ignore */
  }
};

process.on('SIGINT', () => {
  cleanup();
  process.exit(130);
});

try {
  if (!existsSync(mockPath)) {
    throw new Error(`Ficheiro mock não encontrado: ${mockPath}`);
  }

  console.log('A regenerar template base (sem imagens)...');
  execSync('node docs/scripts/build-dossie-simplix-template-dados.mjs --no-images', {
    cwd: root,
    stdio: 'inherit'
  });
  console.log('  →', dadosSemImagensPath);

  const raw = JSON.parse(readFileSync(mockPath, 'utf-8'));
  const payload = stripMetaKeys(raw);

  console.log('A subir API em', base, '(FallbackOnly, sem migrações)...');
  await waitForHttp(`${base}/`);
  console.log('API pronta.');

  const loginRes = await fetch(`${base}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ username: 'docengine.demo', password: 'DocEngine@2025' })
  });
  const loginJson = await loginRes.json();
  if (!loginRes.ok || !loginJson.sucesso) {
    throw new Error(`Login: ${loginRes.status} ${JSON.stringify(loginJson)}`);
  }
  const token = loginJson.resultado?.access_token ?? loginJson.resultado?.accessToken;
  if (!token) throw new Error('Sem token: ' + JSON.stringify(loginJson));

  const { pdfPath, requestJsonPath: reqPath } = await generatePdfFromMock(token, dadosSemImagensPath, payload);
  console.log('Mock:', mockPath);
  console.log('JSON pedido generate-sync:', reqPath, `(${Math.round(readFileSync(reqPath).length / 1024)} KB)`);
  console.log('PDF:', pdfPath);
} catch (e) {
  console.error(e.message || e);
  console.error('--- últimos logs dotnet ---\n', bootLog.slice(-4000));
  process.exitCode = 1;
} finally {
  cleanup();
  await new Promise((r) => setTimeout(r, 800));
}
