#!/usr/bin/env node
/**
 * Corre localmente, sem depender de Postgres na porta 5432:
 * 1) Regenera dados com IMAGENS (docs/preview/dossie-simplix-template-dados.json) e sem-imagens (templates/)
 * 2) Grava docs/preview/dossie-simplix-generate-sync.request.json (corpo POST /documents/generate-sync)
 * 3) Sobe a API (FallbackOnly) e gera docs/preview/dossie-simplix-smoke-api.pdf
 *
 * Uso:
 *   node docs/scripts/run-local-dossie-pdf.mjs
 *   LOCAL_DOCENGINE_NO_IMAGES=1 node docs/scripts/run-local-dossie-pdf.mjs   # só texto (sem ficheiros em preview/extracted-images)
 *   LOCAL_DOCENGINE_PORT=5320 node docs/scripts/run-local-dossie-pdf.mjs
 *   DOSSIE_USE_RASTER_INTERCALADO_ONLY=1 ...  # não envia pdfIntercaladoBase64 (só HTML/JPEG em dados, comportamento antigo)
 *
 * Integração (equipa): PDFs finais típicos em pdfsAnexosBase64 — Termos/Política Simplix + CCB; documentos RG/CNH como imagens
 * em dados (IMG_DOCUMENTO_*). Ver docs/scripts/estrutura-dossie-to-flat-dados.mjs e docs/samples/dossie-payload-estrutura-equipe.exemplo.json
 */
import { spawn, execSync } from 'child_process';
import { readFileSync, writeFileSync, existsSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { randomUUID } from 'crypto';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..', '..');
const docs = join(root, 'docs');
const port = process.env.LOCAL_DOCENGINE_PORT || '5317';
const base = `http://127.0.0.1:${port}`;
const csproj = join(root, 'backend', 'KYX.DocEngine.API', 'KYX.DocEngine.API.csproj');
const requestJsonPath = join(docs, 'preview', 'dossie-simplix-generate-sync.request.json');
const dadosComImagensPath = join(docs, 'preview', 'dossie-simplix-template-dados.json');
const dadosSemImagensPath = join(docs, 'templates', 'dossie-simplix-template-dados.sem-imagens.json');
const imgDir = join(docs, 'preview', 'extracted-images');
/** PDF nativo fundido no sítio de {{DOSSIE_BLOCO_INTERCALADO_HTML}} (POST generate-sync: pdfIntercaladoBase64). */
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

function hasExtractedImages() {
  const need = ['page1_img1.png', 'page1_img2.jpeg', 'page11_img1.jpeg', 'page23_img1.jpeg'];
  return need.every((f) => existsSync(join(imgDir, f)));
}

async function generatePdf(token, envelopePath) {
  const html = readFileSync(join(docs, 'templates', 'template-dossie-simplix.html'), 'utf-8');
  const envelope = JSON.parse(readFileSync(envelopePath, 'utf-8'));
  const dados = { ...envelope.dados };
  const body = {
    requisicaoId: randomUUID(),
    nomeArquivo: 'dossie-simplix-smoke-api.pdf',
    inlineTemplate: { type: 'html', content: html, requiredFields: [] },
    dados
  };
  if (process.env.DOSSIE_USE_RASTER_INTERCALADO_ONLY !== '1' && existsSync(demoIntercaladoPdfPath)) {
    body.pdfIntercaladoBase64 = readFileSync(demoIntercaladoPdfPath).toString('base64');
    dados.DOSSIE_BLOCO_INTERCALADO_HTML = '';
  }
  // Uma linha: o pedido com imagens pode ter vários MB
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
  const out = join(docs, 'preview', 'dossie-simplix-smoke-api.pdf');
  writeFileSync(out, Buffer.from(b64, 'base64'));
  return { pdfPath: out, requestJsonPath };
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
  const forceNoImages = process.env.LOCAL_DOCENGINE_NO_IMAGES === '1';
  const wantImages = !forceNoImages && hasExtractedImages();

  console.log('A regenerar dados mock...');
  execSync('node docs/scripts/build-dossie-simplix-template-dados.mjs --no-images', {
    cwd: root,
    stdio: 'inherit'
  });
  console.log('  →', dadosSemImagensPath);

  let envelopePath = dadosSemImagensPath;
  if (wantImages) {
    execSync('node docs/scripts/build-dossie-simplix-template-dados.mjs', { cwd: root, stdio: 'inherit' });
    envelopePath = dadosComImagensPath;
    console.log('  →', dadosComImagensPath, '(com imagens para o PDF)');
  } else if (!forceNoImages) {
    console.warn(
      '  Aviso: sem ficheiros em docs/preview/extracted-images/ — PDF sem imagens. Defina LOCAL_DOCENGINE_NO_IMAGES=1 para ocultar este aviso.'
    );
  }

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

  const { pdfPath, requestJsonPath: reqPath } = await generatePdf(token, envelopePath);
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
