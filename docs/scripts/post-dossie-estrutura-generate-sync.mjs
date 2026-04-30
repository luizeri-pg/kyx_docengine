#!/usr/bin/env node
/**
 * Sem --template-slug: converte payload estruturado → dados planos + pdfsAnexos e chama
 * POST /documents/generate-sync (motor HTML→PDF local).
 *
 * Com --template-slug (ex. dossie-simplix-v3): envia POST /documents/generate com **dados aninhados**
 * (o DocEngine mapeia no servidor). Referência manual do corpo: docs/preview/mock-kit/dossie-simplix-api-request.mock.json
 *
 * Por defeito grava o último pedido em docs/preview/mock-kit/dossie-simplix-api-request.last-run.json
 * (não sobrescreve o .mock.json).
 *
 * Pré-requisitos generate-sync: Documents:AllowSyncPdfGeneration=true (Development).
 *
 * Uso:
 *   node docs/scripts/post-dossie-estrutura-generate-sync.mjs
 *   node docs/scripts/post-dossie-estrutura-generate-sync.mjs --input caminho/payload.json
 *   BASE_URL=http://127.0.0.1:3000 DOCENGINE_USERNAME=docengine.demo DOCENGINE_PASSWORD='…' node …
 *
 * Saída sem --template-slug: docs/preview/dossie-estrutura-generate-sync.request.json
 *
 * Com --template-slug:
 *   - docs/preview/dossie-estrutura-generate-v2.request.json — cópia do input (contrato BD / meta opcional)
 *   - mock-kit/dossie-simplix-api-request.last-run.json — último POST /documents/generate gravado
 *
 *   --request-out / --estrutura-out sobrepõem; --estrutura-out false desliga a cópia BD.
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { randomUUID } from 'crypto';
import {
  buildTemplateDadosFromEstrutura,
  buildPdfsAnexosForApi,
  stripCcbCredEmitCorrespDoDadosSeSemCamposCcb
} from './estrutura-dossie-to-flat-dados.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(__dirname, '..', '..');
const docs = path.join(root, 'docs');

function parseArgs(argv) {
  const o = {
    input: path.join(docs, 'samples', 'dossie-payload-simplix-nested.exemplo.json'),
    semImagens: path.join(docs, 'templates', 'dossie-simplix-template-dados.sem-imagens.json'),
    html: path.join(docs, 'templates', 'template-dossie-simplix.html'),
    baseUrl: (process.env.BASE_URL || 'http://127.0.0.1:3000').replace(/\/$/, ''),
    requestOut: path.join(docs, 'preview', 'dossie-estrutura-generate-sync.request.json'),
    /** BD: JSON aninhado (sample); só preenchido com --template-slug */
    estruturaOut: undefined,
    outPdf: path.join(docs, 'preview', 'dossie-estrutura-mock-api.pdf'),
    skipPost: process.env.SKIP_HTTP === '1',
    /** Se definido, tenta primeiro POST /documents/generate com config.template */
    templateSlug: process.env.DOSSIE_TEMPLATE_SLUG?.trim() || null,
    requestOutExplicit: false
  };
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--input' && argv[i + 1]) o.input = path.resolve(process.cwd(), argv[++i]);
    else if (a === '--sem-imagens' && argv[i + 1]) o.semImagens = path.resolve(process.cwd(), argv[++i]);
    else if (a === '--base-url' && argv[i + 1]) o.baseUrl = String(argv[++i]).replace(/\/$/, '');
    else if (a === '--request-out' && argv[i + 1]) {
      o.requestOut = path.resolve(process.cwd(), argv[++i]);
      o.requestOutExplicit = true;
    } else if (a === '--estrutura-out' && argv[i + 1]) {
      const raw = String(argv[++i]).trim();
      const low = raw.toLowerCase();
      o.estruturaOut = low === 'false' || low === '0' ? null : path.resolve(process.cwd(), raw);
    } else if (a === '--out-pdf' && argv[i + 1]) o.outPdf = path.resolve(process.cwd(), argv[++i]);
    else if (a === '--skip-http') o.skipPost = true;
    else if (a === '--template-slug' && argv[i + 1]) o.templateSlug = String(argv[++i]).trim();
  }

  if (!o.templateSlug && process.env.DOSSIE_TEMPLATE_SLUG?.trim()) {
    o.templateSlug = process.env.DOSSIE_TEMPLATE_SLUG.trim();
  }

  if (o.templateSlug && !o.requestOutExplicit) {
    o.requestOut = path.join(docs, 'preview', 'mock-kit', 'dossie-simplix-api-request.last-run.json');
  }

  if (o.estruturaOut === undefined) {
    o.estruturaOut = o.templateSlug ? path.join(docs, 'preview', 'dossie-estrutura-generate-v2.request.json') : null;
  }

  return o;
}

function loadStringFieldsFromJson(filePath) {
  const j = JSON.parse(fs.readFileSync(filePath, 'utf8'));
  const o = {};
  const mergeStrings = (obj) => {
    if (!obj || typeof obj !== 'object' || Array.isArray(obj)) return;
    for (const [k, v] of Object.entries(obj)) {
      if (k.startsWith('_')) continue;
      if (typeof v === 'string') o[k] = v;
    }
  };
  mergeStrings(j);
  mergeStrings(j.dados);
  return o;
}

async function login(baseUrl) {
  const u = process.env.DOCENGINE_USERNAME;
  const p = process.env.DOCENGINE_PASSWORD;
  if (!u || !p) {
    throw new Error('Defina DOCENGINE_USERNAME e DOCENGINE_PASSWORD (ou exporte TOKEN=JWT e adapte o script).');
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

async function postGenerateSync(opts, token, dados, pdfsAnexos) {
  const html = fs.readFileSync(opts.html, 'utf8');
  const body = {
    requisicaoId: randomUUID(),
    nomeArquivo: 'dossie-estrutura-mock.pdf',
    inlineTemplate: { type: 'html', content: html, requiredFields: [] },
    dados,
    ...(pdfsAnexos.length > 0 ? { pdfsAnexos } : {})
  };
  const syncPath = opts.requestOut.replace(/\.json$/i, '') + '-sync.request.json';
  fs.mkdirSync(path.dirname(syncPath), { recursive: true });
  fs.writeFileSync(syncPath, JSON.stringify(body, null, 2) + '\n', 'utf8');
  console.log('Pedido generate-sync gravado:', syncPath);

  const res = await fetch(`${opts.baseUrl}/documents/generate-sync`, {
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
    throw new Error(`generate-sync: ${res.status} ${JSON.stringify(json).slice(0, 2000)}`);
  }
  const b64 = json.resultado?.base64;
  if (!b64) {
    throw new Error(`generate-sync sem base64: ${JSON.stringify(json).slice(0, 1500)}`);
  }
  return b64;
}

async function main() {
  const opts = parseArgs(process.argv);
  const fileRoot = JSON.parse(fs.readFileSync(opts.input, 'utf8'));
  if (opts.estruturaOut) {
    fs.mkdirSync(path.dirname(opts.estruturaOut), { recursive: true });
    fs.writeFileSync(opts.estruturaOut, JSON.stringify(fileRoot, null, 2) + '\n', 'utf8');
    console.log('Payload estruturado (contrato BD):', opts.estruturaOut);
  }

  /** Suporta input só com `dados` aninhados OU corpo completo POST (mock-kit .mock.json). */
  let presetEnvelope = null;
  let nestedSource = fileRoot;
  if (
    fileRoot &&
    typeof fileRoot === 'object' &&
    fileRoot.dados &&
    typeof fileRoot.dados === 'object' &&
    !Array.isArray(fileRoot.dados) &&
    fileRoot.config &&
    typeof fileRoot.config === 'object'
  ) {
    presetEnvelope = {
      requisicaoId: typeof fileRoot.requisicaoId === 'string' ? fileRoot.requisicaoId : null,
      config: { ...fileRoot.config }
    };
    nestedSource = fileRoot.dados;
  }

  const estrutura = { ...nestedSource };
  delete estrutura.meta;

  const sem = loadStringFieldsFromJson(opts.semImagens);
  const fromEstrutura = buildTemplateDadosFromEstrutura(estrutura);
  const dados = { ...sem, ...fromEstrutura };
  stripCcbCredEmitCorrespDoDadosSeSemCamposCcb(estrutura, dados);

  const logo = String(dados.LOGO ?? '').trim();
  if (!logo) {
    dados.LOGO =
      'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';
  }

  const pdfsAnexos = buildPdfsAnexosForApi(estrutura);

  fs.mkdirSync(path.dirname(opts.requestOut), { recursive: true });

  let bodyGen = null;
  let bodySync = null;
  if (opts.templateSlug) {
    const centro = (process.env.CENTRO_CUSTO || 'DEMO').trim();
    const slugTail = String(opts.templateSlug).replace(/^dossie-/, '');
    const nomeArquivo = (process.env.NOME_ARQUIVO_PDF || `dossie-${slugTail}-mock.pdf`).trim();
    /** Mesmo contrato que docs/preview/mock-kit/dossie-simplix-api-request.mock.json — dados aninhados; anexos vêm de dados.anexosPdf. */
    const cfgFromCli = {
      template: opts.templateSlug,
      centroCusto: centro,
      nomeArquivo
    };
    bodyGen = presetEnvelope
      ? {
          requisicaoId: presetEnvelope.requisicaoId || randomUUID(),
          config: {
            ...presetEnvelope.config,
            template: opts.templateSlug,
            centroCusto:
              process.env.CENTRO_CUSTO?.trim() || presetEnvelope.config.centroCusto || cfgFromCli.centroCusto,
            nomeArquivo:
              process.env.NOME_ARQUIVO_PDF?.trim() || presetEnvelope.config.nomeArquivo || cfgFromCli.nomeArquivo
          },
          dados: estrutura
        }
      : {
          requisicaoId: randomUUID(),
          config: cfgFromCli,
          dados: estrutura
        };
    fs.writeFileSync(opts.requestOut, JSON.stringify(bodyGen, null, 2) + '\n', 'utf8');
    console.log('Pedido POST /documents/generate gravado:', opts.requestOut);
    console.log('Template:', opts.templateSlug, '| dados: aninhados (como mock-kit)', '| PDFs em dados.anexosPdf:', pdfsAnexos.length);
  } else {
    const html = fs.readFileSync(opts.html, 'utf8');
    bodySync = {
      requisicaoId: randomUUID(),
      nomeArquivo: 'dossie-estrutura-mock.pdf',
      inlineTemplate: { type: 'html', content: html, requiredFields: [] },
      dados,
      ...(pdfsAnexos.length > 0 ? { pdfsAnexos } : {})
    };
    fs.writeFileSync(opts.requestOut, JSON.stringify(bodySync, null, 2) + '\n', 'utf8');
    console.log('Pedido gravado:', opts.requestOut);
    console.log('pdfsAnexos:', pdfsAnexos.length);
  }

  if (opts.skipPost) {
    console.log('SKIP_HTTP=1 ou --skip-http: não enviou à API.');
    return;
  }

  const token = process.env.TOKEN?.trim() || (await login(opts.baseUrl));

  if (opts.templateSlug) {
    const res = await fetch(`${opts.baseUrl}/documents/generate`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
        Authorization: `Bearer ${token}`
      },
      body: JSON.stringify(bodyGen)
    });
    const json = await res.json();
    if (res.ok && json.sucesso && json.resultado?.base64) {
      fs.writeFileSync(opts.outPdf, Buffer.from(json.resultado.base64, 'base64'));
      console.log('PDF (via template API):', opts.outPdf, 'bytes:', fs.statSync(opts.outPdf).size);
      return;
    }
    console.warn('POST /documents/generate falhou — fallback generate-sync.', res.status, JSON.stringify(json).slice(0, 1200));
  } else {
    const res = await fetch(`${opts.baseUrl}/documents/generate-sync`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
        Authorization: `Bearer ${token}`
      },
      body: JSON.stringify(bodySync)
    });
    const json = await res.json();
    if (!res.ok || !json.sucesso) {
      console.error('Falha generate-sync:', res.status, JSON.stringify(json).slice(0, 2000));
      process.exit(1);
    }
    const b64 = json.resultado?.base64;
    if (!b64) {
      console.error('Resposta sem resultado.base64:', JSON.stringify(json).slice(0, 1500));
      process.exit(1);
    }
    fs.writeFileSync(opts.outPdf, Buffer.from(b64, 'base64'));
    console.log('PDF:', opts.outPdf, 'bytes:', fs.statSync(opts.outPdf).size);
    return;
  }

  const b64 = await postGenerateSync(opts, token, dados, pdfsAnexos);
  fs.writeFileSync(opts.outPdf, Buffer.from(b64, 'base64'));
  console.log('PDF (fallback generate-sync):', opts.outPdf, 'bytes:', fs.statSync(opts.outPdf).size);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
