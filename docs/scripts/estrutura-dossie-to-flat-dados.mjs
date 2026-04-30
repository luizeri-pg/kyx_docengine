/**
 * Converte o payload “estruturado” (arrays + Base64) nas chaves string planas
 * que o motor substitui em {{CHAVE}} no template-dossie-simplix.html.
 *
 * Uso no integrador (Node):
 *   import { buildTemplateDadosFromEstrutura } from './estrutura-dossie-to-flat-dados.mjs';
 *   const dados = buildTemplateDadosFromEstrutura(JSON.parse(fs.readFileSync('payload.json')));
 *   // POST /documents/generate(-sync): pdfsAnexos: [{ ordem, base64 }] (preferido) ou pdfsAnexosBase64: string[]
 *
 * Trilha: array `trilhaEventos[]` **ou** objeto `{ ordem?, eventos: [...] }` (mesma ordem do array `eventos`).
 * `identificacao` ou `identificacaoHtml` (coluna Identificação).
 * Anexos PDF: `anexosPdf`: `[{ ordem: 1, base64: "..." }, { ordem: 2, base64: "..." }]` — ordem define a sequência no PDF final (qualquer tipo de documento).
 * Legado: `termoUso`/`ccb` com `base64` ainda é aceite.
 * `provaDeVida`: `{ nome, base64 }`; `documento`: `{ rotulo?, tipo?, base64 }` (`rotulo` ou `tipo` → DOCUMENTO_TIPO no HTML).
 * `logsCabecalho`: `{ nome?, protocoloAtendimento, dataHoraInicioAtendimento }` — nome no cabeçalho dos logs (se omitido, usa cliente.nome).
 * `logsInteracao`: array de linhas **ou** objeto `{ protocolo?, dataHoraInicio?, registros: [...] }` — **todo** `registros[]` na ordem do array.
 * `anexosPdf[]`: `{ ordem, base64, rotulo?, tipo?, dadosCliente?, nome? }` — a ordem no PDF segue `ordem`.
 * Imagens no corpo do HTML (sem `provaDeVida`/`documento` no raiz): entre os itens que forem **raster** (PNG/JPEG, não PDF),
 *   o **1.º** na ordem preenche `IMG_SELFIE` (e `PROVA_VIDA_NOME` a partir de `dadosCliente.nome` ou `nome` do item, se vazio);
 *   o **2.º** preenche `IMG_DOCUMENTO_FRENTE`/`IMG_DOCUMENTO_VERSO` e `DOCUMENTO_TIPO` a partir de `tipo` ou `rotulo`.
 * Opcional: `imagemParaChaves` / `mapearBase64Para`, `textoParaChaves` / `camposTemplate` — mapeamento explícito (sobrepõe só chaves ainda vazias).
 * `ordemSecoes`: `{ cliente: 1, captura: 2, trilhaEventos: 3, logsIntegracao: 4, ... }` — só contrato de API/tela; **não** é enviado ao template (não entra em `dados`).
 *
 * O DocEngine em C# também aceita JSON aninhado via FlattenToStringDictionary, mas o HTML
 * precisa de EVENTOS_HTML e INTERACOES_HTML já como fragmentos <tr>...</tr> — por isso este passo.
 * Termos/CCB em PDF: preferir `anexosPdf` / `config.pdfsAnexos`. Mesmo assim, `DOSSIE_BLOCO_INTERCALADO_HTML`
 * e `TERMOS_POLITICA_HTML` são sempre preenchidos a partir do JSON aninhado (ou `''`), para não herdarem
 * o demo gigante de `dossie-simplix-template-dados.sem-imagens.json` no merge `{ ...sem, ...fromEstrutura }`.
 */
import { escapeHtml } from './html-escape.mjs';

function idLine(ip, lat, lon, ua) {
  const i = escapeHtml(String(ip ?? ''));
  const la = escapeHtml(String(lat ?? ''));
  const lo = escapeHtml(String(lon ?? ''));
  const u = escapeHtml(String(ua ?? ''));
  return `<span class="id-line">IP: ${i}</span><span class="id-line">Latitude: ${la}</span><span class="id-line">Longitude: ${lo}</span><span class="id-line">Celular e Navegador: ${u}</span>`;
}

/** Coluna Identificação: `identificacaoHtml` (HTML confiável) ou `identificacao` (texto escapado) ou fallback IP/geo/UA da captura. */
function buildIdentificacaoCell(row, ctx) {
  if (row.identificacaoHtml != null && String(row.identificacaoHtml).trim() !== '') {
    return String(row.identificacaoHtml);
  }
  if (row.identificacao != null && String(row.identificacao).trim() !== '') {
    return escapeHtml(String(row.identificacao));
  }
  return idLine(ctx.ip, ctx.lat, ctx.lon, ctx.ua);
}

/** Três colunas no PDF: Evento | Data | Identificação. */
function evtRow(evento, dataHoraHtml, identificacaoCell) {
  return `<tr><td>${escapeHtml(String(evento ?? ''))}</td><td>${dataHoraHtml}</td><td>${identificacaoCell}</td></tr>`;
}

function interacaoRow(dataHora, tipo, texto) {
  return `<tr><td>${dataHora}</td><td>${escapeHtml(String(tipo ?? ''))}</td><td>${texto}</td></tr>`;
}

/** Array de eventos ou `{ ordem?, eventos: [...] }` (API/tela). */
function normalizeTrilhaEventosRows(e) {
  const te = e.trilhaEventos;
  if (te == null) return [];
  if (Array.isArray(te)) return [...te];
  if (typeof te === 'object' && Array.isArray(te.eventos)) return [...te.eventos];
  if (typeof te === 'object' && Array.isArray(te.itens)) return [...te.itens];
  return [];
}

/** Array de linhas ou `{ registros: [...] }` (API/tela). */
function normalizeLogsInteracaoRows(e) {
  const li = e.logsInteracao;
  if (li == null) return [];
  if (Array.isArray(li)) return [...li];
  if (typeof li === 'object' && Array.isArray(li.registros)) return [...li.registros];
  return [];
}

/** Aceita Base64 cru ou data URI; devolve valor aceite pelo HtmlPdfRenderer (data URI ou cru nas chaves de imagem). */
function normalizeImageDataUri(base64OrDataUri, mimeFallback = 'image/jpeg') {
  if (base64OrDataUri == null || String(base64OrDataUri).trim() === '') return '';
  const s = String(base64OrDataUri).trim();
  if (s.startsWith('data:')) return s;
  const compact = s.replace(/\s/g, '');
  let mime = mimeFallback;
  if (compact.startsWith('iVBOR')) mime = 'image/png';
  else if (compact.startsWith('/9j/')) mime = 'image/jpeg';
  else if (compact.startsWith('R0lGOD')) mime = 'image/gif';
  else if (compact.startsWith('UklGR')) mime = 'image/webp';
  return `data:${mime};base64,${compact}`;
}

/** Base64 de PDF (anexo nativo), não raster. */
function looksLikePdfBase64(s) {
  const c = String(s ?? '')
    .trim()
    .replace(/\s/g, '');
  if (!c) return false;
  if (/^data:application\/pdf/i.test(c)) return true;
  return c.startsWith('JVBERi');
}

/** PNG/JPEG/GIF em base64 cru ou data URI de imagem. */
function looksLikeRasterImageBase64(s) {
  const c = String(s ?? '')
    .trim()
    .replace(/\s/g, '');
  if (!c) return false;
  if (/^data:image\//i.test(c)) return true;
  return /^(iVBOR|\/9j\/|R0lGOD|UklGR)/.test(c);
}

function coalesceChavesTemplateLista(v) {
  if (v == null) return [];
  if (Array.isArray(v)) return v.filter((x) => typeof x === 'string' && x.trim() !== '');
  if (typeof v === 'string' && v.trim() !== '') return [v.trim()];
  return [];
}

function firstNonEmptyString(...candidates) {
  for (const c of candidates) {
    if (c != null && String(c).trim() !== '') return String(c);
  }
  return '';
}

/** HTML opcional do bloco intercalado (vários nomes aceites no payload aninhado). */
function pickDossieBlocoIntercaladoHtml(e) {
  return firstNonEmptyString(
    e.dossieBlocoIntercaladoHtml,
    e.blocoIntercaladoHtml,
    e.dossie_bloco_intercalado_html,
    e.bloco_intercalado_html
  );
}

/** Termos/política em HTML (vários nomes aceites). */
function pickTermosPoliticaHtml(e) {
  return firstNonEmptyString(
    e.termosPoliticaHtml,
    e.termosPoliticaHTML,
    e.termos_politica_html,
    e.termoPoliticaHtml
  );
}

/**
 * Preenche chaves do template a partir de `anexosPdf[]` quando o integrador declara o mapeamento
 * (nada depende do texto de `rotulo`).
 */
function fillFromAnexosPdfDeclarative(e, out) {
  const lista = e.anexosPdf;
  if (!Array.isArray(lista)) return;
  const sorted = [...lista].sort((a, b) => (Number(a?.ordem) || 0) - (Number(b?.ordem) || 0));
  const docKeys = new Set(['IMG_DOCUMENTO_FRENTE', 'IMG_DOCUMENTO_VERSO']);

  for (const item of sorted) {
    const chavesImg = coalesceChavesTemplateLista(item.imagemParaChaves ?? item.mapearBase64Para);
    const b64 = item?.base64;
    if (chavesImg.length && b64 && looksLikeRasterImageBase64(b64) && !looksLikePdfBase64(b64)) {
      const img = normalizeImageDataUri(b64);
      for (const k of chavesImg) {
        if (!k || typeof k !== 'string') continue;
        if (!String(out[k] ?? '').trim()) out[k] = img;
      }
      if (chavesImg.some((k) => docKeys.has(k)) && !String(out.DOCUMENTO_TIPO ?? '').trim()) {
        const t = item.tipo ?? item.rotulo;
        if (t != null && String(t).trim() !== '') out.DOCUMENTO_TIPO = String(t).trim();
      }
      if (chavesImg.includes('IMG_SELFIE') && !String(out.PROVA_VIDA_NOME ?? '').trim()) {
        const n = item.dadosCliente?.nome ?? item.nome;
        if (n != null && String(n).trim() !== '') out.PROVA_VIDA_NOME = String(n).trim();
      }
    }

    const txtMap = item.textoParaChaves ?? item.camposTemplate;
    if (txtMap && typeof txtMap === 'object' && !Array.isArray(txtMap)) {
      for (const [k, v] of Object.entries(txtMap)) {
        if (!k || v == null) continue;
        if (!String(out[k] ?? '').trim()) {
          out[k] = typeof v === 'string' ? v : String(v);
        }
      }
    }
  }
}

/**
 * 1.º anexo raster (por `ordem`) → selfie; 2.º → documento. PDFs (JVBERi…) são ignorados nesta lista.
 * Só preenche chaves ainda vazias (compatível com `provaDeVida`/`documento` no raiz ou mapeamento explícito).
 */
function fillRasterImagesFromAnexosByOrder(e, out) {
  const lista = e.anexosPdf;
  if (!Array.isArray(lista)) return;
  const sorted = [...lista].sort((a, b) => (Number(a?.ordem) || 0) - (Number(b?.ordem) || 0));
  const rasters = sorted.filter(
    (item) => item?.base64 && looksLikeRasterImageBase64(item.base64) && !looksLikePdfBase64(item.base64)
  );
  if (rasters.length === 0) return;

  if (!String(out.IMG_SELFIE ?? '').trim() && rasters[0]) {
    out.IMG_SELFIE = normalizeImageDataUri(rasters[0].base64);
    if (!String(out.PROVA_VIDA_NOME ?? '').trim()) {
      const n = rasters[0].dadosCliente?.nome ?? rasters[0].nome;
      if (n != null && String(n).trim() !== '') out.PROVA_VIDA_NOME = String(n).trim();
    }
  }
  if (!String(out.IMG_DOCUMENTO_FRENTE ?? '').trim() && rasters[1]) {
    const img = normalizeImageDataUri(rasters[1].base64);
    out.IMG_DOCUMENTO_FRENTE = img;
    out.IMG_DOCUMENTO_VERSO = img;
    if (!String(out.DOCUMENTO_TIPO ?? '').trim()) {
      const t = rasters[1].tipo ?? rasters[1].rotulo;
      if (t != null && String(t).trim() !== '') out.DOCUMENTO_TIPO = String(t).trim();
    }
  }
}

/** Remove prefixo data:application/pdf;base64, se existir (API espera só Base64 do PDF). */
export function stripPdfDataUriToBase64(s) {
  if (s == null || String(s).trim() === '') return '';
  const t = String(s).trim();
  const m = /^data:application\/pdf[^;]*;base64,(.+)$/is.exec(t);
  return (m ? m[1] : t).replace(/\s/g, '');
}

/**
 * Monta lista de Base64 na ordem de `anexosPdf[].ordem` (para `pdfsAnexosBase64` legado).
 * Prioridade: `e.anexosPdf` (array de `{ ordem, base64 }`) → ordena por `ordem`;
 * senão legado `termoUso` + `ccb`; senão objeto `pdfsAnexos` com chaves fixas.
 */
export function buildPdfsAnexosTermosPoliticaECcb(e) {
  const lista = e.anexosPdf ?? e.pdfsAnexosOrdenados;
  if (Array.isArray(lista) && lista.length > 0) {
    const copia = lista
      .map((item, idx) => ({
        ordem: typeof item?.ordem === 'number' ? item.ordem : idx + 1,
        b64: stripPdfDataUriToBase64(item?.base64)
      }))
      .filter((x) => x.b64.length > 0)
      .sort((a, b) => a.ordem - b.ordem);
    return copia.map((x) => x.b64);
  }

  const tNovo = stripPdfDataUriToBase64(e.termoUso?.base64 ?? e.termosUso?.base64);
  const cNovo = stripPdfDataUriToBase64(e.ccb?.base64 ?? e.ccBase?.base64);
  if (tNovo || cNovo) {
    const o = [];
    if (tNovo) o.push(tNovo);
    if (cNovo) o.push(cNovo);
    return o;
  }

  const p = e.pdfsAnexos ?? e.anexosPdf;
  if (!p || typeof p !== 'object' || Array.isArray(p)) return [];
  const termos =
    p.termosUsoPoliticaPrivacidadeBase64 ??
    p.termosPoliticaPdfBase64 ??
    p.termosPdfBase64;
  const ccb = p.ccbBase64 ?? p.cedulaCreditoBancarioBase64 ?? p.ccbPdfBase64;
  const out = [];
  for (const raw of [termos, ccb]) {
    const b64 = stripPdfDataUriToBase64(raw);
    if (b64) out.push(b64);
  }
  return out;
}

/**
 * PDFs nativos para `config.pdfsAnexos` (POST /documents/generate): cada item tem `ordem` + `base64`,
 * espelhando `anexosPdf[].ordem`. O DocEngine ordena por `ordem` antes de anexar ao PDF principal.
 */
export function buildPdfsAnexosForApi(e) {
  const lista = e.anexosPdf ?? e.pdfsAnexosOrdenados;
  if (Array.isArray(lista) && lista.length > 0) {
    const copia = lista
      .map((item, idx) => ({
        ordem: typeof item?.ordem === 'number' && !Number.isNaN(item.ordem) ? item.ordem : idx + 1,
        idx,
        base64: stripPdfDataUriToBase64(item?.base64)
      }))
      .filter((x) => x.base64.length > 0 && looksLikePdfBase64(x.base64))
      .sort((a, b) => {
        if (a.ordem !== b.ordem) return a.ordem - b.ordem;
        return a.idx - b.idx;
      });
    return copia.map(({ ordem, base64 }) => ({ ordem, base64 }));
  }

  const flat = buildPdfsAnexosTermosPoliticaECcb(e).filter((b64) => looksLikePdfBase64(b64));
  return flat.map((base64, i) => ({ ordem: i + 1, base64 }));
}

/**
 * @param {object} e — payload da equipa
 * @returns {Record<string, string>} dados planos para o template
 */
export function buildTemplateDadosFromEstrutura(e) {
  const cliente = e.cliente ?? {};
  const captura = e.captura ?? {};
  const ctx = {
    ip: captura.ip ?? e.dispositivo?.ip ?? '',
    lat: captura.latitude ?? e.dispositivo?.latitude ?? '',
    lon: captura.longitude ?? e.dispositivo?.longitude ?? '',
    ua: captura.userAgent ?? e.dispositivo?.userAgent ?? ''
  };

  const trilhaRaw = normalizeTrilhaEventosRows(e);
  const EVENTOS_HTML = trilhaRaw
    .map((row) => {
      const colData =
        row.dataHoraHtml != null ? String(row.dataHoraHtml) : escapeHtml(String(row.dataHora ?? ''));
      const identCell = buildIdentificacaoCell(row, ctx);
      return evtRow(row.evento ?? row.tipo ?? row.nome, colData, identCell);
    })
    .join('\n');

  const logsRaw = normalizeLogsInteracaoRows(e);
  const INTERACOES_HTML = logsRaw
    .map((row) => {
      const colData =
        row.dataHoraHtml != null ? String(row.dataHoraHtml) : escapeHtml(String(row.dataHora ?? ''));
      const colTexto =
        row.interacaoHtml != null
          ? String(row.interacaoHtml)
          : escapeHtml(String(row.texto ?? row.interacao ?? ''));
      return interacaoRow(colData, row.tipo ?? row.origem ?? '', colTexto);
    })
    .join('\n');

  const lh = e.logsCabecalho ?? {};
  const liObj =
    e.logsInteracao != null && typeof e.logsInteracao === 'object' && !Array.isArray(e.logsInteracao)
      ? e.logsInteracao
      : null;
  const nomeClienteExibicao = String(
    lh.nome ?? lh.nomeCliente ?? cliente.nome ?? ''
  );

  const out = {
    CLIENTE_NOME: nomeClienteExibicao,
    CLIENTE_CPF: String(cliente.cpf ?? ''),
    CLIENTE_NASCIMENTO: String(cliente.nascimento ?? ''),
    CAPTURA_DATA_HORA: String(captura.dataHora ?? ''),
    CAPTURA_LATITUDE: String(captura.latitude ?? ''),
    CAPTURA_LONGITUDE: String(captura.longitude ?? ''),
    CAPTURA_IP: String(captura.ip ?? ''),
    CAPTURA_PORTA: String(captura.porta ?? ''),
    CAPTURA_MODELO_OS: String(captura.modeloOs ?? captura.modeloOS ?? ''),
    PROTOCOLO_ATENDIMENTO: String(
      lh.protocoloAtendimento ??
        lh.protocolo ??
        e.protocoloAtendimento ??
        (liObj?.protocolo != null ? String(liObj.protocolo) : '')
    ),
    ATENDIMENTO_INICIO: String(
      lh.dataHoraInicioAtendimento ??
        lh.inicioAtendimento ??
        e.atendimentoInicio ??
        (liObj?.dataHoraInicio != null ? String(liObj.dataHoraInicio) : '')
    ),
    EVENTOS_HTML,
    INTERACOES_HTML,
    IMG_CLIENTE_FOTO: normalizeImageDataUri(cliente.fotoBase64 ?? cliente.fotoEmBase64)
  };

  const prova = e.provaDeVida;
  if (prova && typeof prova === 'object') {
    out.PROVA_VIDA_NOME = String(prova.nome ?? cliente.nome ?? '');
    out.IMG_SELFIE = normalizeImageDataUri(prova.base64 ?? prova.imagemBase64);
  } else {
    out.PROVA_VIDA_NOME = String(cliente.nome ?? '');
    out.IMG_SELFIE = normalizeImageDataUri(e.provaDeVidaBase64 ?? e.imgSelfieBase64);
  }

  if (e.documento?.base64) {
    const img = normalizeImageDataUri(e.documento.base64);
    out.IMG_DOCUMENTO_FRENTE = img;
    out.IMG_DOCUMENTO_VERSO = img;
    const rotuloOuTipo = e.documento.rotulo ?? e.documento.tipo;
    if (rotuloOuTipo != null && String(rotuloOuTipo).trim() !== '') {
      out.DOCUMENTO_TIPO = String(rotuloOuTipo);
    }
  }

  const doc = e.documentos ?? e.documentoIdentificacao;
  if (doc && typeof doc === 'object') {
    const f = doc.frenteBase64 ?? doc.frenteEmBase64;
    const v = doc.versoBase64 ?? doc.versoEmBase64;
    if (f) out.IMG_DOCUMENTO_FRENTE = normalizeImageDataUri(f);
    if (v) out.IMG_DOCUMENTO_VERSO = normalizeImageDataUri(v);
    const r = doc.rotulo ?? doc.tipo;
    if (r != null && String(r).trim() !== '' && !out.DOCUMENTO_TIPO) {
      out.DOCUMENTO_TIPO = String(r);
    }
  }

  if (e.ccbCampos && typeof e.ccbCampos === 'object') {
    Object.assign(out, flattenPrefix(e.ccbCampos, 'CCB'));
  } else if (e.ccb && typeof e.ccb === 'object') {
    const { base64: _ccbPdf, ...restCcb } = e.ccb;
    if (Object.keys(restCcb).length > 0) {
      Object.assign(out, flattenPrefix(restCcb, 'CCB'));
    }
  }
  if (e.hashDossie != null) {
    out.HASH_DOSSIE = String(e.hashDossie);
  }
  if (e.docengineUseChromePageFooter != null) {
    out.DOCENGINE_USE_CHROME_PAGE_FOOTER = String(e.docengineUseChromePageFooter);
  }

  fillFromAnexosPdfDeclarative(e, out);
  fillRasterImagesFromAnexosByOrder(e, out);

  const extras = e.templateExtras ?? e.camposExtrasTemplate;
  if (extras && typeof extras === 'object') {
    for (const [k, v] of Object.entries(extras)) {
      if (v == null) continue;
      if (typeof v === 'string') {
        if (/^IMG_/.test(k) && v.trim() !== '' && looksLikeRasterImageBase64(v)) {
          out[k] = normalizeImageDataUri(v);
        } else {
          out[k] = v;
        }
      } else {
        out[k] = JSON.stringify(v);
      }
    }
  }

  out.DOSSIE_BLOCO_INTERCALADO_HTML = pickDossieBlocoIntercaladoHtml(e);
  out.TERMOS_POLITICA_HTML = pickTermosPoliticaHtml(e);

  const marca = e.marca != null && typeof e.marca === 'object' ? e.marca : null;
  const logoRaw =
    e.logoBase64 ??
    e.logo ??
    e.logoSimplixBase64 ??
    (marca != null ? marca.logoBase64 ?? marca.logo : undefined);
  if (logoRaw != null && String(logoRaw).trim() !== '') {
    out.LOGO = normalizeImageDataUri(String(logoRaw));
  }
  const tituloRaw =
    e.titulo ??
    e.tituloDossie ??
    e.tituloCabecalho ??
    e.headerTitulo ??
    (marca != null ? marca.titulo ?? marca.tituloDossie ?? marca.tituloCabecalho : undefined);
  if (tituloRaw != null && String(tituloRaw).trim() !== '') {
    out.DOSSIE_HEADER_TITULO = String(tituloRaw);
  }

  return out;
}

function flattenPrefix(obj, prefix) {
  const o = {};
  for (const [k, v] of Object.entries(obj)) {
    const key = `${prefix}_${k.toUpperCase()}`;
    o[key] = v == null ? '' : typeof v === 'string' ? v : String(v);
  }
  return o;
}

/** Chaves de `ccbCampos` / `ccb` que só representam PDF/anexo, não quadro contratual. */
function normCcbKey(k) {
  return String(k).toLowerCase().replace(/_/g, '');
}

const CCB_SOLO_ANEXO_KEYS = new Set([
  'base64',
  'documentobase64',
  'contratobase64',
  'ccbcontratobase64',
  'ordem',
  'rotulo'
]);

/**
 * Há dados de contrato/CCB no payload estruturado (além de PDF em `ccb` / anexos).
 */
export function hasStructuredCcbCamposPayload(e) {
  if (!e || typeof e !== 'object') return false;
  const cc = e.ccbCampos;
  if (cc && typeof cc === 'object' && !Array.isArray(cc)) {
    const meaningful = Object.keys(cc).filter((k) => !CCB_SOLO_ANEXO_KEYS.has(normCcbKey(k)));
    if (meaningful.length > 0) return true;
  }
  const ccb = e.ccb;
  if (ccb && typeof ccb === 'object' && !Array.isArray(ccb)) {
    const restKeys = Object.keys(ccb).filter((k) => !CCB_SOLO_ANEXO_KEYS.has(normCcbKey(k)));
    if (restKeys.length > 0) return true;
  }
  return false;
}

/**
 * Remove chaves de demo do `sem-imagens.json` quando o nested não traz `ccbCampos` (evita CCB/CREDOR/EMITENTE/CORRESP de outro cliente).
 * @param {object} estrutura — payload aninhado (mesmo que `e` em buildTemplateDadosFromEstrutura)
 * @param {Record<string, string>} dados — objeto mutável após `{ ...sem, ...fromEstrutura }`
 */
export function stripCcbCredEmitCorrespDoDadosSeSemCamposCcb(estrutura, dados) {
  if (hasStructuredCcbCamposPayload(estrutura)) return;
  const prefixes = ['CCB_', 'CREDOR_', 'EMITENTE_', 'CORRESP_'];
  for (const key of Object.keys(dados)) {
    if (prefixes.some((p) => key.startsWith(p))) delete dados[key];
  }
}
