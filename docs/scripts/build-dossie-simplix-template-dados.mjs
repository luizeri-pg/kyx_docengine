#!/usr/bin/env node
/**
 * Gera JSON de `dados` para preencher `docs/templates/template-dossie-simplix.html`
 * (mesmo conteúdo textual/HTML do mock / PDF original Caroline).
 *
 * Uso:
 *   node docs/scripts/build-dossie-simplix-template-dados.mjs
 *     → docs/preview/dossie-simplix-template-dados.json (com imagens em data URI)
 *
 *   node docs/scripts/build-dossie-simplix-template-dados.mjs --no-images
 *     → docs/templates/dossie-simplix-template-dados.sem-imagens.json (leve, versionável)
 *
 * Paginação: o template quebra secções com CSS; tabelas e texto legal fluem entre folhas.
 * Envie DOCENGINE_USE_CHROME_PAGE_FOOTER=true — o motor PDF (Chromium) desenha hash + k/N no rodapé.
 */
import { readFileSync, writeFileSync, existsSync } from 'fs';
import { execFileSync } from 'child_process';
import { join, dirname, relative } from 'path';
import { fileURLToPath } from 'url';
import { createMinimalOnePagePdf } from './minimal-pdf.mjs';
import {
  HASH,
  stdIp,
  stdLat,
  stdLon,
  stdUa,
  allEvents,
  allInteractions,
  termosP1,
  termosP2,
  termosP3,
  ccbSections
} from './dossie-simplix-shared-data.mjs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const docsDir = join(__dirname, '..');
const imgDir = join(docsDir, 'preview', 'extracted-images');
const repoRoot = join(docsDir, '..');
const demoFidelizzaPdfPath = join(docsDir, 'fidelizza-2025-split', 'demo-generate-response.pdf');
const mockIntercaladoPdfPath = join(docsDir, 'preview', 'mock-kit', 'anexo-proposta-mock.pdf');

/**
 * HTML de teste para DOSSIE_BLOCO_INTERCALADO_HTML: embute um PDF (data URI) entre logs e prova de vida
 * (no layout antigo era por volta da “página 13”; agora é posição fixa no HTML, folhas calculadas pelo Chrome).
 *
 * Ordem: DOSSIE_INTERCALADO_PDF (absoluto ou relativo à raiz do repo) → docs/fidelizza-2025-split/demo-generate-response.pdf
 * → mock-kit/anexo-proposta-mock.pdf → PDF mínimo. DOSSIE_SKIP_INTERCALADO_PDF_TEST=1 deixa vazio.
 *
 * Para o PDF aparecer no Chrome→PDF, a 1.ª página é rasterizada com PyMuPDF (pip install pymupdf)
 * via docs/scripts/pdf-first-page-to-png-b64.py; se falhar, usa-se &lt;embed&gt; (muitas vezes não imprime).
 * Em produção prefira POST com <code>pdfIntercaladoBase64</code> + <code>DOSSIE_BLOCO_INTERCALADO_HTML</code> vazio (PDF nativo no slot).
 */
/** Rasteriza página 1 (JPEG, largura máx. 1100px). Tenta python3 e python. */
function tryFirstPageImageDataUri(pdfPath) {
  const py = join(docsDir, 'scripts', 'pdf-first-page-to-png-b64.py');
  if (!existsSync(py)) return null;
  const args = [py, pdfPath, '--jpeg', '--max-width', '1100'];
  let b64 = null;
  for (const bin of ['python3', 'python']) {
    try {
      b64 = execFileSync(bin, args, {
        encoding: 'utf8',
        maxBuffer: 40 * 1024 * 1024,
        stdio: ['ignore', 'pipe', 'pipe']
      }).trim();
      if (b64 && b64.length > 80) break;
    } catch {
      b64 = null;
    }
  }
  if (!b64) return null;
  return `data:image/jpeg;base64,${b64}`;
}

function buildIntercaladoPdfHtmlForTest() {
  if (process.env.DOSSIE_SKIP_INTERCALADO_PDF_TEST === '1') {
    return '';
  }
  const envPath = process.env.DOSSIE_INTERCALADO_PDF?.trim();
  const candidates = [];
  if (envPath) {
    candidates.push(envPath.startsWith('/') ? envPath : join(repoRoot, envPath));
  }
  candidates.push(demoFidelizzaPdfPath, mockIntercaladoPdfPath);

  let chosenPath;
  for (const p of candidates) {
    if (existsSync(p)) {
      chosenPath = p;
      break;
    }
  }

  if (!chosenPath) {
    const buf = createMinimalOnePagePdf('PDF intercalado — teste KYX DocEngine');
    const b64 = buf.toString('base64');
    const origem = 'PDF mínimo em memória (nenhum dos PDFs de teste encontrado no disco)';
    return `<div class="dossie-section"><div class="section-title">Documento intercalado</div><embed type="application/pdf" src="data:application/pdf;base64,${b64}" style="width:100%;min-height:720px;border:1px solid #ccc;" /></div>`;
  }

  const imgUri = tryFirstPageImageDataUri(chosenPath);
  if (imgUri) {
    return `<div class="dossie-section"><div class="section-title">Documento intercalado</div><img src="${imgUri}" alt="Anexo" width="1100" style="max-width:100%;height:auto;border:1px solid #ccc;display:block;" /></div>`;
  }

  const buf = readFileSync(chosenPath);
  const b64 = buf.toString('base64');
  return `<div class="dossie-section"><div class="section-title">Documento intercalado</div><embed type="application/pdf" src="data:application/pdf;base64,${b64}" style="width:100%;min-height:720px;border:1px solid #ccc;" /></div>`;
}

function imgB64(filename) {
  const buf = readFileSync(join(imgDir, filename));
  const ext = filename.split('.').pop().toLowerCase();
  const mime = ext === 'png' ? 'image/png' : 'image/jpeg';
  return `data:${mime};base64,${buf.toString('base64')}`;
}

function idLine(ip, lat, lon, ua) {
  return `<span class="id-line">IP: ${ip}</span><span class="id-line">Latitude: ${lat}</span><span class="id-line">Longitude: ${lon}</span><span class="id-line">Celular e Navegador: ${ua}</span>`;
}

function evtRow(name, date, identificacao) {
  const idCol = identificacao ?? idLine(stdIp, stdLat, stdLon, stdUa);
  return `<tr><td>${name}</td><td>${date}</td><td>${idCol}</td></tr>`;
}

function buildDados(includeImages) {
  const logo = includeImages ? imgB64('page1_img1.png') : '';
  const fotoCliente = includeImages ? imgB64('page1_img2.jpeg') : '';
  const selfie = includeImages ? imgB64('page11_img1.jpeg') : '';
  const docImg = includeImages ? imgB64('page23_img1.jpeg') : '';

  const EVENTOS_HTML = allEvents.map((e) => evtRow(e[0], e[1], idLine(stdIp, stdLat, stdLon, stdUa))).join('\n');

  const INTERACOES_HTML = allInteractions
    .map((r) => `<tr><td>${r[0]}</td><td>${r[1]}</td><td>${r[2]}</td></tr>`)
    .join('\n');

  const ccbPageContents = [ccbSections[0], ccbSections[1], ccbSections[2] + ccbSections[3]];
  const continPlaceholder =
    '<p><i>[Continuação das condições gerais da CCB – texto legal completo]</i></p>'.repeat(3);
  const CCB_CONDICOES_GERAIS_HTML = ccbPageContents.join('') + continPlaceholder;

  const CCB_SECAO_IV_HTML = `<p>Data/Prazo para Liberação do Valor Líquido do Empréstimo: 01(hum) dia útil contado da assinatura desta CCB por todas as Partes bem como, adicionalmente e contado do cumprimento total das condições precedentes abaixo marcadas, as quais foram devidamente anuídas pelas Partes:</p><p>(i) a confirmação ao CREDOR, pelo agente operador do FGTS, da disponibilidade de Direitos de Saque Aniversário suficientes para pagamento da(s) Parcela(s) da CCB e</p><p>(ii) a efetiva realização de bloqueio, por comando do CREDOR ou eventual CORRESPONDENTE BANCÁRIO assim habilitado, aceito pelo operador do FGTS, dos Direitos de Saque Aniversário em valor correspondente à(s) Parcela(s) da CCB.</p>`;

  const CCB_SECAO_V_HTML = `<p>Considerando a entrega, pelo EMITENTE, em cessão fiduciária ao CREDOR, dos Direitos de Saque Aniversário, o pagamento da(s) parcela(s) devidas pelo EMITENTE conforme o quadro FLUXO DE PAGAMENTOS ("Parcela(s)"), se dará mediante a cobrança e recebimento, pelo CREDOR, dos Direitos de Saque Aniversário diretamente da Caixa Econômica Federal ("Agente operador do FGTS").</p><p>Caso o Agente Operador do FGTS não repasse ao CREDOR os valores dos Direitos de Saque Aniversário em valor suficiente para a quitação da(s) Parcela(s), total ou parcialmente, independentemente da razão, o EMITENTE está ciente de que não estará desobrigado de pagar ao CREDOR.</p>`;

  const CCB_SECAO_VI_HTML = `<p>Sem prejuízo das condições previstas na Clausula "GARANTIA(S) desta CCB, o EMITENTE constitui em favor das obrigações garantidas lastreadas pela presente CCB, a cessão fiduciária de todos os direitos creditórios, principais e acessórios, presentes e futuros, de saque dos recursos depositados nas contas vinculadas de titularidade do EMITENTE no Fundo de Garantia por Tempo de Serviço ("FGTS"), disponíveis e bloqueados por meio da sistemática do Saque-Aniversário.</p>`;

  const CCB_PARCELAS_HTML = `<tr><td>001</td><td>01/09/2026</td><td>R$ 207,82</td><td>179,78</td><td>28,04</td></tr><tr><td>002</td><td>01/09/2027</td><td>R$ 122,62</td><td>86,40</td><td>36,21</td></tr>`;

  const documentacao = {
    templateArquivo: 'docs/templates/template-dossie-simplix.html',
    slugSugeridoApi: 'dossie-simplix',
    incluiImagens: includeImages,
    usoApi:
      'No POST /documents/generate envie apenas requisicaoId, config e dados. O DocEngine substitui cada {{CHAVE}} pelo valor string de dados.CHAVE (objeto achatado se vier aninhado). O objeto _documentacao não vai para a API — é só guia.',

    paginacao: {
      modo:
        'Secções usam .dossie-section (quebra antes de cada bloco, exceto a primeira). Tabelas e texto legal fluem: o Chrome repete thead e parte conteúdo entre folhas.',
      rodapeAutomatico:
        'Defina DOCENGINE_USE_CHROME_PAGE_FOOTER=true nos dados. O HtmlPdfRenderer usa o rodapé nativo do Chromium: HASH_DOSSIE + página/total calculados na impressão (sem PAGINA_TOTAL nem HTML extra por página).',
      chavesHtmlFluxo:
        'EVENTOS_HTML e INTERACOES_HTML: todas as linhas <tr>; CCB_CONDICOES_GERAIS_HTML: corpo legal contínuo (sem div.page por folha).'
    },

    ficheirosImagensEBase64: {
      ondeVao:
        'No objeto dados, como strings nos mesmos nomes do template (ex.: LOGO_SIMPLIX_BASE64). Não é um anexo multipart: é texto no JSON.',
      chavesDeImagemNoTemplate: [
        'LOGO_SIMPLIX_BASE64',
        'IMG_CLIENTE_FOTO',
        'IMG_SELFIE',
        'IMG_DOCUMENTO_FRENTE',
        'IMG_DOCUMENTO_VERSO'
      ],
      formatosAceitesPeloDocEngine:
        'Data URI completa (data:image/png;base64,...) ou só o Base64 da imagem (sem prefixo) — o API normaliza para data URI nas chaves de imagem acima. URL https também funciona se o Chrome conseguir ir buscar na impressão.',
      exemploNode:
        "import fs from 'fs'; const b64 = fs.readFileSync('logo.png').toString('base64'); dados.LOGO_SIMPLIX_BASE64 = 'data:image/png;base64,' + b64;",
      pdfsAdicionaisNoFinalDoDossie:
        'Ficheiros PDF completos (ex.: proposta) não entram em dados: use config.pdfsAnexosBase64 no POST /documents/generate — array de strings, cada uma um PDF inteiro em Base64, anexados após o HTML→PDF.'
    },

    conteudoIntercaladoNoMeioDoDossie: {
      chave: 'DOSSIE_BLOCO_INTERCALADO_HTML',
      posicaoNoTemplate: 'Depois da secção Logs, antes de Prova de vida (posição fixa no documento; o nº da folha no PDF é automático).',
      formato:
        'Uma única string HTML ou vazia. Podes incluir imagens com data URI (data:image/png;base64,...), várias <img>, tabelas, etc. Para “um PDF aqui”: o mais fiável é rasterizar páginas do PDF para PNG e usar <img>; <embed src="data:application/pdf;base64,..."> pode ou não renderizar bem no Chrome→PDF.',
      exemploMinimo:
        '<div class="dossie-section"><div class="section-title">Comprovante anexo</div><img src="data:image/png;base64,SEU_BASE64" alt="" style="max-width:100%;" /></div>',
      testeAutomaticoNesteScript:
        'Por defeito usa docs/fidelizza-2025-split/demo-generate-response.pdf (1.ª página rasterizada com PyMuPDF para aparecer no PDF; fallback mock-kit ou PDF mínimo). DOSSIE_INTERCALADO_PDF=caminho/outro.pdf. DOSSIE_SKIP_INTERCALADO_PDF_TEST=1 vazio.',
      exemploNodePdfExistente:
        "import fs from 'fs'; const b64 = fs.readFileSync('meu.pdf').toString('base64'); dados.DOSSIE_BLOCO_INTERCALADO_HTML = '<div class=\"dossie-section\"><embed type=\"application/pdf\" src=\"data:application/pdf;base64,' + b64 + '\" style=\"width:100%;min-height:600px\" /></div>';"
    },

    ordemDasSecoesNoHtmlDoTemplate: [
      { passo: 1, descricao: 'Cliente, captura, trilha (tabela completa)', chaves: ['LOGO_*', 'IMG_CLIENTE_FOTO', 'CLIENTE_*', 'CAPTURA_*', 'EVENTOS_HTML', 'HASH_DOSSIE', 'DOCENGINE_USE_CHROME_PAGE_FOOTER'] },
      { passo: 2, descricao: 'Logs', chaves: ['INTERACOES_HTML', 'PROTOCOLO_ATENDIMENTO', 'ATENDIMENTO_INICIO'] },
      { passo: 3, descricao: 'Opcional: anexo no fluxo (HTML / imagem Base64)', chaves: ['DOSSIE_BLOCO_INTERCALADO_HTML'] },
      { passo: 4, descricao: 'Prova de vida', chaves: ['PROVA_VIDA_NOME', 'IMG_SELFIE'] },
      { passo: 5, descricao: 'Termos', chaves: ['TERMOS_POLITICA_HTML'] },
      { passo: 6, descricao: 'CCB partes e operações', chaves: ['CCB_*', 'CREDOR_*', 'EMITENTE_*', 'CORRESP_*', 'CCB_PARCELAS_HTML'] },
      { passo: 7, descricao: 'Condições gerais VIII', chaves: ['CCB_CONDICOES_GERAIS_HTML'] },
      { passo: 8, descricao: 'Documentos', chaves: ['DOCUMENTO_TIPO', 'IMG_DOCUMENTO_FRENTE', 'IMG_DOCUMENTO_VERSO'] }
    ]
  };

  const dados = {
    LOGO_SIMPLIX_BASE64: logo,
    IMG_CLIENTE_FOTO: fotoCliente,
    IMG_SELFIE: selfie,
    IMG_DOCUMENTO_FRENTE: docImg,
    IMG_DOCUMENTO_VERSO: docImg,
    HASH_DOSSIE: HASH,
    CLIENTE_NOME: 'CAROLINE MOREIRA DOS SANTOS',
    PROVA_VIDA_NOME: 'CAROLINE MOREIRA DOS SANTOS',
    CLIENTE_CPF: '449.361.358-01',
    CLIENTE_NASCIMENTO: '18/09/1998',
    CAPTURA_DATA_HORA: '18/12/2025 13:33:52 (-03:00)',
    CAPTURA_LATITUDE: stdLat,
    CAPTURA_LONGITUDE: stdLon,
    CAPTURA_IP: '189.76.168.193',
    CAPTURA_PORTA: '60992',
    CAPTURA_MODELO_OS: 'Linux; Android 10; K',
    DOCENGINE_USE_CHROME_PAGE_FOOTER: 'true',
    EVENTOS_HTML: EVENTOS_HTML,
    PROTOCOLO_ATENDIMENTO: '8da24334-17a7-4950-94af-507260c8e97c',
    ATENDIMENTO_INICIO: '18/12/2025 13:29:43',
    INTERACOES_HTML: INTERACOES_HTML,
    DOSSIE_BLOCO_INTERCALADO_HTML: buildIntercaladoPdfHtmlForTest(),
    TERMOS_POLITICA_HTML: `${termosP1}${termosP2}${termosP3}`,
    CCB_NUMERO: '75297688',
    CCB_DATA_EMISSAO: '17/12/2025',
    CCB_FINALIDADE: 'EP - ANTECIPAÇÃO DE SAQUE ANIVERSÁRIO',
    CREDOR_RAZAO_SOCIAL: 'BMP SOCIEDADE DE CREDITO DIRETO S.A',
    CREDOR_CNPJ: '34.337.707/0001-00',
    CREDOR_ENDERECO: 'AVENIDA PAULISTA, 1294, 6º ANDAR - BELA VISTA',
    CREDOR_CEP: '01310-100',
    CREDOR_CIDADE: 'SÃO PAULO',
    CREDOR_UF: 'SP',
    EMITENTE_NOME: 'CAROLINE MOREIRA DOS SANTOS',
    EMITENTE_CPF: '449.361.358-01',
    EMITENTE_ENDERECO: 'RUA ANTÔNIO SOUZA MELLO, 579, A - JARDIM PARQUE DAS CEREJEIRAS',
    EMITENTE_MUNICIPIO: 'SARANDI',
    EMITENTE_UF: 'PR',
    EMITENTE_CEP: '87118-352',
    EMITENTE_DOCUMENTO: '50527823',
    EMITENTE_ORGAO_EXPEDIDOR: '',
    EMITENTE_EMAIL: 'carolmoreira816@gmail.com',
    EMITENTE_TELEFONE: '44 92001-3037',
    EMITENTE_CELULAR: '',
    EMITENTE_ESTADO_CIVIL: 'Casado, Sim',
    EMITENTE_CONJUGE: '',
    CORRESP_RAZAO_SOCIAL: 'SIMPLIX PROMOTORA DE VENDAS LTDA',
    CORRESP_CNPJ: '57.777.846/0001-50',
    CORRESP_ENDERECO:
      'ALAMEDA MAMORÉ, 687, 5º ANDAR - ALPHAVILLE CENTRO INDUSTRIAL E EMPRESARIAL/ALPHAVILLE.',
    CORRESP_MUNICIPIO: 'BARUERI',
    CORRESP_UF: 'SP',
    CORRESP_CEP: '06454-040',
    CCB_VALOR_LIQUIDO: '195,53',
    CCB_VALOR_IOF: '7,41',
    CCB_TARIFA_CADASTRO: '62,87',
    CCB_TAXA_MENSAL: '1,69990',
    CCB_TAXA_ANUAL: '22,42',
    CCB_DESPESAS: '0,37',
    CCB_CET_MENSAL: '4,38',
    CCB_CET_ANUAL: '67,17',
    CCB_CET_VALOR: '134,91',
    CCB_VALOR_SEM_JUROS: '266,18',
    CCB_VALOR_COM_JUROS: '330,44',
    CCB_DATA_VENCIMENTO: '01/09/2026',
    CCB_PRACA_PAGAMENTO: 'BARUERI',
    CCB_BANCO: '237',
    CCB_AGENCIA: '36',
    CCB_CONTA: '40213-3 / Conta Corrente',
    CCB_SECAO_IV_HTML: CCB_SECAO_IV_HTML,
    CCB_SECAO_V_HTML: CCB_SECAO_V_HTML,
    CCB_SECAO_VI_HTML: CCB_SECAO_VI_HTML,
    CCB_PARCELAS_HTML: CCB_PARCELAS_HTML,
    CCB_CONDICOES_GERAIS_HTML: CCB_CONDICOES_GERAIS_HTML,
    DOCUMENTO_TIPO: 'CNH'
  };

  return { documentacao, dados };
}

const noImages = process.argv.includes('--no-images');
const { documentacao, dados } = buildDados(!noImages);
const envelope = { _documentacao: documentacao, dados };

if (noImages) {
  const out = join(docsDir, 'templates', 'dossie-simplix-template-dados.sem-imagens.json');
  writeFileSync(out, JSON.stringify(envelope, null, 2), 'utf-8');
  console.log('Escrito (sem imagens):', out);
} else {
  const out = join(docsDir, 'preview', 'dossie-simplix-template-dados.json');
  writeFileSync(out, JSON.stringify(envelope), 'utf-8');
  console.log('Escrito (com imagens):', out);
  console.log('Tamanho aprox. (bytes):', Buffer.byteLength(JSON.stringify(envelope)));
}
