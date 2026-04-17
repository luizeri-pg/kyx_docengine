import { readFileSync, writeFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { execSync } from 'child_process';
import {
  HASH,
  stdIp,
  stdLat,
  stdLon,
  stdUa,
  allEvents,
  allInteractions,
  fmt,
  termosP1,
  termosP2,
  termosP3,
  ccbSections
} from './dossie-simplix-shared-data.mjs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const docsDir = join(__dirname, '..');
const imgDir = join(docsDir, 'preview', 'extracted-images');

/* ── Imagens reais como base64 ── */
function imgB64(filename) {
  const buf = readFileSync(join(imgDir, filename));
  const ext = filename.split('.').pop().toLowerCase();
  const mime = ext === 'png' ? 'image/png' : 'image/jpeg';
  return `data:${mime};base64,${buf.toString('base64')}`;
}

const logoB64 = imgB64('page1_img1.png');
const fotoClienteB64 = imgB64('page1_img2.jpeg');
const selfieB64 = imgB64('page11_img1.jpeg');
const docImageB64 = imgB64('page23_img1.jpeg');
const bmpLogoB64 = imgB64('page15_img1.png');

const idLine = (ip, lat, lon, ua) =>
  `<span class="id-line">IP: ${ip}</span><span class="id-line">Latitude: ${lat}</span><span class="id-line">Longitude: ${lon}</span><span class="id-line">Celular e Navegador: ${ua}</span>`;

function footer(pageNum, totalPages) {
  return `<div class="page-footer-relative"><span class="hash">${HASH}</span><span class="pnum">${pageNum}/${totalPages}</span></div>`;
}

function ccbCombinedFooter(pageNum, totalPages, ccbPageNum, ccbTotalPages) {
  return `<div class="page-footer-relative" style="flex-direction:column;align-items:stretch;">
    <div class="ccb-footer-via"><span>Esta página é parte integrante da Cédula de Crédito Bancário nº 75297688, tendo como Emitente CAROLINE MOREIRA DOS SANTOS e CPF/CNPJ: 449.361.358-01</span><span class="ccb-footer-via-right">Página ${ccbPageNum} de ${ccbTotalPages}</span></div>
    <div style="display:flex;justify-content:space-between;align-items:flex-end;margin-top:6px;"><span class="hash">${HASH}</span><span class="pnum">${pageNum}/${totalPages}</span></div>
  </div>`;
}

function evtRow(name, date) {
  return `<tr><td>${name}</td><td>${date}</td><td>${idLine(stdIp, stdLat, stdLon, stdUa)}</td></tr>`;
}

function evtTableHeader() {
  return `<table class="events-table"><thead><tr><th class="col-evento">Evento</th><th class="col-data">Data e hora (GMT)</th><th class="col-id">Identificação</th></tr></thead><tbody>`;
}

function logTableHeader() {
  return `<table class="logs-table"><thead><tr><th class="col-data-log">Data e hora (GMT)</th><th class="col-tipo">Tipo</th><th class="col-interacao">Interação</th></tr></thead><tbody>`;
}

/* ── Construir todas as páginas ── */
const TOTAL_PAGES = 23;
const pages = [];

// --- PÁGINA 1: Header + Cliente + Captura ---
pages.push(`<div class="page">
  <div class="header"><img class="header-logo" src="${logoB64}" alt="Simplix" /><span class="header-title">Dossiê probatório – Contratação Digital Simplix</span></div>
  <div class="section-title">Dados do cliente</div>
  <div class="client-block">
    <div class="client-photo"><img src="${fotoClienteB64}" alt="Foto" /></div>
    <div class="client-fields">
      <div><span class="field-label">Nome do cliente</span><span class="field-value">CAROLINE MOREIRA DOS SANTOS</span></div>
      <div><span class="field-label">CPF</span><span class="field-value">449.361.358-01</span></div>
      <div><span class="field-label">Data de Nascimento</span><span class="field-value">18/09/1998</span></div>
    </div>
  </div>
  <div class="section-title">Dados da captura</div>
  <div class="capture-grid">
    <div class="capture-row">
      <div class="capture-cell"><span class="field-label">Data e Hora</span><span class="field-value-normal">18/12/2025 13:33:52 (-03:00)</span></div>
      <div class="capture-cell"><span class="field-label">Latitude</span><span class="field-value-normal">-23.4188809</span></div>
      <div class="capture-cell"><span class="field-label">Longitude</span><span class="field-value-normal">-51.9370214</span></div>
    </div>
    <div class="capture-row">
      <div class="capture-cell"><span class="field-label">IP</span><span class="field-value-normal">189.76.168.193</span></div>
      <div class="capture-cell"><span class="field-label">Porta Lógica</span><span class="field-value-normal">60992</span></div>
    </div>
    <div><span class="field-label">Modelo/OS</span><span class="field-value-normal">Linux; Android 10; K</span></div>
  </div>
  ${footer(1, TOTAL_PAGES)}
</div>`);

// --- PÁGINAS 2-4: Trilha de eventos (5 por página como o original) ---
const evtChunks = [
  allEvents.slice(0, 5),    // pg2: 5 eventos (com header "Trilha de eventos")
  allEvents.slice(5, 10),   // pg3: 5 eventos
  allEvents.slice(10, 15)   // pg4: 5 eventos
];
evtChunks.forEach((chunk, idx) => {
  const rows = chunk.map(e => evtRow(e[0], e[1])).join('\n');
  const header = idx === 0 ? '<div class="section-title" style="margin-top:0;">Trilha de eventos</div>' : '';
  pages.push(`<div class="page">${header}${evtTableHeader()}${rows}</tbody></table>${footer(idx + 2, TOTAL_PAGES)}</div>`);
});

// --- PÁGINAS 6-11: Logs das interações ---
const logChunks = [
  allInteractions.slice(0, 1),     // pg6: saudação longa (com header)
  allInteractions.slice(1, 5),     // pg7: CONTINUAR + escolha + CONTINUAR_CNH + upload
  allInteractions.slice(5, 6),     // pg8: proposta longa
  allInteractions.slice(6, 9),     // pg9: checkbox + CONTINUAR + selfie/UNICO
  allInteractions.slice(9, 11),    // pg10: CONTINUAR + enquadre
  allInteractions.slice(11, 13)    // pg11: aguarde + conclusão
];

const LOG_START_PAGE = 5;
logChunks.forEach((chunk, idx) => {
  const rows = chunk.map(r => `<tr><td>${r[0]}</td><td>${r[1]}</td><td>${r[2]}</td></tr>`).join('\n');
  let content = '';
  if (idx === 0) {
    content = `<div class="section-title" style="margin-top:0;">Logs das integrações com o cliente</div>
    <div class="logs-header">
      <div><span class="field-label">Nome do cliente</span><span class="field-value">CAROLINE MOREIRA DOS SANTOS</span></div>
      <div class="field-row" style="display:flex;gap:40px;margin-top:10px;">
        <div><span class="field-label">Protocolo de atendimento</span><span class="field-value-normal">8da24334-17a7-4950-94af-507260c8e97c</span></div>
        <div><span class="field-label">Data e hora início atendimento</span><span class="field-value-normal">18/12/2025 13:29:43</span></div>
      </div>
    </div>
    <hr class="logs-separator" />`;
  }
  content += `${logTableHeader()}${rows}</tbody></table>`;
  pages.push(`<div class="page">${content}${footer(LOG_START_PAGE + idx, TOTAL_PAGES)}</div>`);
});

// --- PÁGINA 11: Prova de vida ---
pages.push(`<div class="page">
  <div class="section-title" style="margin-top:0;">Prova de vida</div>
  <div style="margin-bottom:10px;"><span class="field-label">Nome do cliente</span><span class="field-value">CAROLINE MOREIRA DOS SANTOS</span></div>
  <div class="img-container"><img src="${selfieB64}" alt="Prova de vida" /></div>
  ${footer(11, TOTAL_PAGES)}
</div>`);

// --- PÁGINAS 12-14: Termos de Uso (3 páginas) ---
pages.push(`<div class="page"><div style="font-size:16px;font-weight:700;margin-bottom:14px;">Termos de Uso e Política de Privacidade Simplix</div><div class="legal-body">${termosP1}</div>${footer(12, TOTAL_PAGES)}</div>`);
pages.push(`<div class="page"><div class="legal-body">${termosP2}</div>${footer(13, TOTAL_PAGES)}</div>`);
pages.push(`<div class="page"><div class="legal-body">${termosP3}</div>${footer(14, TOTAL_PAGES)}</div>`);

// --- PÁGINA 15: CCB Partes + Operação (layout com tabelas bordadas como original) ---
const cell = (lbl, val, colspan) => {
  const cs = colspan ? ` colspan="${colspan}"` : '';
  return `<td${cs}><span class="lbl">${lbl}</span><span class="val">${val}</span></td>`;
};
const cellB = (lbl, val, colspan) => {
  const cs = colspan ? ` colspan="${colspan}"` : '';
  return `<td${cs}><span class="lbl">${lbl}</span><span class="val-bold">${val}</span></td>`;
};

pages.push(`<div class="page">
  <div class="ccb-header"><img class="ccb-header-logo" src="${bmpLogoB64}" alt="BMP" /><div class="ccb-header-via">VIA NEGOCIÁVEL</div></div>
  <div class="ccb-title">CÉDULA DE CRÉDITO BANCÁRIO</div>
  <table class="ccb-data"><tr>
    ${cellB('CÉDULA DE CREDITO Nº','75297688')}
    ${cellB('DATA DE EMISSÃO','17/12/2025')}
    ${cellB('FINALIDADE DE CRÉDITO','EP - ANTECIPAÇÃO DE SAQUE ANIVERSÁRIO')}
  </tr></table>
  <div class="ccb-section-num">I. PARTES</div>
  <div class="ccb-sub-title">CREDOR</div>
  <table class="ccb-data">
    <tr>${cell('Razão Social','BMP SOCIEDADE DE CREDITO DIRETO S.A',3)}${cell('CNPJ','34.337.707/0001-00')}</tr>
    <tr>${cell('Endereço','AVENIDA PAULISTA, 1294, 6º ANDAR - BELA VISTA')}${cell('CEP','01310-100')}${cell('Cidade','SÃO PAULO')}${cell('UF','SP')}</tr>
  </table>
  <div class="ccb-sub-title">EMITENTE</div>
  <table class="ccb-data">
    <tr>${cell('Nome/Razão Social','CAROLINE MOREIRA DOS SANTOS',3)}${cell('CPF','449.361.358-01')}</tr>
    <tr>${cell('Endereço','RUA ANTÔNIO SOUZA MELLO, 579, A - JARDIM PARQUE DAS CEREJEIRAS')}${cell('Município','SARANDI')}${cell('UF','PR')}${cell('CEP','87118-352')}</tr>
    <tr>${cell('Documento de Identidade','50527823')}${cell('Órgão Expedidor','',2)}${cell('Data da Expedição','')}</tr>
    <tr>${cell('E-mail','carolmoreira816@gmail.com')}${cell('Telefone Fixo','44 92001-3037')}${cell('Celular','',2)}</tr>
    <tr>${cell('Estado Civil e Regime','Casado, Sim',2)}${cell('Cônjuge e CPF','',2)}</tr>
  </table>
  <div class="ccb-sub-title">CORRESPONDENTE BANCÁRIO</div>
  <table class="ccb-data">
    <tr>${cell('Nome/Razão Social','SIMPLIX PROMOTORA DE VENDAS LTDA',3)}${cell('CNPJ','57.777.846/0001-50')}</tr>
    <tr>${cell('Endereço','ALAMEDA MAMORÉ, 687, 5º ANDAR - ALPHAVILLE CENTRO INDUSTRIAL E EMPRESARIAL/ALPHAVILLE.')}${cell('Município','BARUERI')}${cell('UF','SP')}${cell('CEP','06454-040')}</tr>
  </table>
  <div class="ccb-section-num">II. CARACTERÍSTICAS DA OPERAÇÃO</div>
  <table class="ccb-ops">
    <tr>${cell('Valor Líquido do Empréstimo (R$)','195,53')}${cell('Valor do IOF ( R$)','7,41')}${cell('Tarifa de Cadastro (R$)','62,87')}</tr>
    <tr>${cell('Taxa Efetiva de Juros (a.m.)','1,69990')}${cell('Taxa Efetiva de Juros (a.a.)','22,42')}${cell('Despesas (R$)','0,37')}</tr>
    <tr>${cell('Custo Efetivo Total (a.m.) (%)','4,38')}${cell('Custo Efetivo Total (a.a.)','67,17')}${cell('Custo Efetivo Total (R$)','134,91')}</tr>
  </table>
  <p style="font-size:8px;text-align:center;margin:4px 0;color:#333;">Demonstrativo relacionado ao CET ( atendimento à Resolução 4841/2020)</p>
  <table class="ccb-ops">
    <tr>${cell('Valor Total do Empréstimo (SEM juros)','266,18')}${cell('Valor Total do Empréstimo (COM juros)','330,44')}${cell('Data de Vencimento da CCB','01/09/2026')}</tr>
    <tr>${cell('Praça de Pagamento desta CCB','BARUERI')}${cell('PARCELAS DEVIDAS','Conforme Fluxo previsto no Quadro" FLUXO DE PAGAMENTOS"',2)}</tr>
  </table>
  <div class="ccb-section-num">III - DADOS BANCÁRIOS DO EMITENTE</div>
  <p style="font-size:9px;color:#333;margin:2px 0 4px;">Segue abaixo contemplado, informações apresentadas pelo EMITENTE, indicando os dados bancários para desembolso o Valor Líquido do Empréstimo desta CCB:</p>
  ${ccbCombinedFooter(15, TOTAL_PAGES, 1, 8)}
</div>`);

// --- PÁGINA 16: Banco + Seções IV-VII ---
pages.push(`<div class="page">
  <div class="ccb-header"><img class="ccb-header-logo" src="${bmpLogoB64}" alt="BMP" /><div class="ccb-header-via">VIA NEGOCIÁVEL</div></div>
  <table class="ccb-data">
    <tr>${cell('Banco','237')}${cell('Agência','36')}${cell('Conta Corrente ou Pagamento','40213-3 / Conta Corrente')}</tr>
  </table>
  <div class="ccb-section-num">IV – Informações para Desembolso</div>
  <div class="legal-body"><p>Data/Prazo para Liberação do Valor Líquido do Empréstimo: 01(hum) dia útil contado da assinatura desta CCB por todas as Partes bem como, adicionalmente e contado do cumprimento total das condições precedentes abaixo marcadas, as quais foram devidamente anuídas pelas Partes:</p><p>(i) a confirmação ao CREDOR, pelo agente operador do FGTS, da disponibilidade de Direitos de Saque Aniversário suficientes para pagamento da(s) Parcela(s) da CCB e</p><p>(ii) a efetiva realização de bloqueio, por comando do CREDOR ou eventual CORRESPONDENTE BANCÁRIO assim habilitado, aceito pelo operador do FGTS, dos Direitos de Saque Aniversário em valor correspondente à(s) Parcela(s) da CCB.</p></div>
  <div class="ccb-section-num">V – Forma de Pagamento da CCB</div>
  <div class="legal-body"><p>Considerando a entrega, pelo EMITENTE, em cessão fiduciária ao CREDOR, dos Direitos de Saque Aniversário, o pagamento da(s) parcela(s) devidas pelo EMITENTE conforme o quadro FLUXO DE PAGAMENTOS ("Parcela(s)"), se dará mediante a cobrança e recebimento, pelo CREDOR, dos Direitos de Saque Aniversário diretamente da Caixa Econômica Federal ("Agente operador do FGTS").</p><p>Caso o Agente Operador do FGTS não repasse ao CREDOR os valores dos Direitos de Saque Aniversário em valor suficiente para a quitação da(s) Parcela(s), total ou parcialmente, independentemente da razão, o EMITENTE está ciente de que não estará desobrigado de pagar ao CREDOR.</p></div>
  <div class="ccb-section-num">VI – Garantias</div>
  <div class="legal-body"><p>Sem prejuízo das condições previstas na Clausula "GARANTIA(S) desta CCB, o EMITENTE constitui em favor das obrigações garantidas lastreadas pela presente CCB, a cessão fiduciária de todos os direitos creditórios, principais e acessórios, presentes e futuros, de saque dos recursos depositados nas contas vinculadas de titularidade do EMITENTE no Fundo de Garantia por Tempo de Serviço ("FGTS"), disponíveis e bloqueados por meio da sistemática do Saque-Aniversário.</p></div>
  <div class="ccb-section-num">VII – Fluxo de Pagamentos</div>
  <table class="ccb-data" style="font-size:9px;">
    <thead><tr><td colspan="3" style="font-weight:700;text-align:center;background:#f5f5f5;">Identificação da(s) Parcela(s)</td><td colspan="2" style="font-weight:700;text-align:center;background:#f5f5f5;">Componentes do Valor da Parcela</td></tr>
    <tr><td style="font-weight:700;">Parcela</td><td style="font-weight:700;">Data de Vencimento</td><td style="font-weight:700;">Valor Total da Parcela R$</td><td style="font-weight:700;">Valor de Principal</td><td style="font-weight:700;">Valor de Juros</td></tr></thead>
    <tbody><tr><td>001</td><td>01/09/2026</td><td>R$ 207,82</td><td>179,78</td><td>28,04</td></tr><tr><td>002</td><td>01/09/2027</td><td>R$ 122,62</td><td>86,40</td><td>36,21</td></tr></tbody>
  </table>
  ${ccbCombinedFooter(16, TOTAL_PAGES, 2, 8)}
</div>`);

// --- PÁGINAS 17-19: Condições Gerais ---
// Merge CCB sections into pages (2 sections per page roughly)
const ccbPageContents = [
  ccbSections[0],
  ccbSections[1],
  ccbSections[2] + ccbSections[3]
];

ccbPageContents.forEach((content, idx) => {
  const bmpHeader = `<div class="ccb-header"><img class="ccb-header-logo" src="${bmpLogoB64}" alt="BMP" /><div class="ccb-header-via">VIA NEGOCIÁVEL</div></div>`;
  let sectionHeader = idx === 0 ? '<div class="ccb-section-num">VIII – Condições Gerais – Cédula de Crédito Bancário</div>' : '';
  const ccbPageNum = 3 + idx;
  pages.push(`<div class="page">${bmpHeader}${sectionHeader}<div class="legal-body">${content}</div>${ccbCombinedFooter(17 + idx, TOTAL_PAGES, ccbPageNum, 8)}</div>`);
});

// Fill remaining CCB pages (20-22) with placeholder
for (let p = 20; p <= 22; p++) {
  const bmpHeader = `<div class="ccb-header"><img class="ccb-header-logo" src="${bmpLogoB64}" alt="BMP" /><div class="ccb-header-via">VIA NEGOCIÁVEL</div></div>`;
  const ccbPageNum = p - 14;
  pages.push(`<div class="page">${bmpHeader}<div class="legal-body"><p><i>[Continuação das condições gerais da CCB – texto legal completo]</i></p></div>${ccbCombinedFooter(p, TOTAL_PAGES, ccbPageNum, 8)}</div>`);
}

// --- PÁGINA 23: Documentos ---
pages.push(`<div class="page">
  <div class="section-title" style="margin-top:0;">Documentos</div>
  <p style="font-weight:700;margin-bottom:10px;">CNH</p>
  <div class="img-doc-container"><img src="${docImageB64}" alt="CNH" /></div>
  ${footer(23, TOTAL_PAGES)}
</div>`);

/* ── Montar HTML final ── */
const templatePath = join(docsDir, 'templates', 'template-dossie-simplix.html');
let templateHtml = readFileSync(templatePath, 'utf-8');
const styleMatch = templateHtml.match(/<style>([\s\S]*?)<\/style>/);
const styleBlock = styleMatch ? styleMatch[0] : '';

const finalHtml = `<!DOCTYPE html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8" />
  <title>Dossiê probatório – Contratação Digital Simplix</title>
  ${styleBlock}
</head>
<body>
${pages.join('\n')}
</body>
</html>`;

const resolvedPath = join(docsDir, 'preview', 'dossie-simplix-resolved.html');
writeFileSync(resolvedPath, finalHtml, 'utf-8');
console.log('HTML resolvido salvo em:', resolvedPath);
console.log(`Total de páginas geradas: ${pages.length}`);
console.log('JSON completo do template (dados p/ API): node docs/scripts/build-dossie-simplix-template-dados.mjs');

/* ── Gerar PDF ── */
const pdfPath = join(docsDir, 'preview', 'dossie-simplix-mock.pdf');
const chromeCmd = `"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless --disable-gpu --no-sandbox --no-pdf-header-footer --print-to-pdf="${pdfPath}" "${resolvedPath}"`;

console.log('Gerando PDF com Chrome headless...');
try {
  execSync(chromeCmd, { stdio: 'inherit', timeout: 60000 });
  console.log('PDF gerado com sucesso:', pdfPath);
} catch (err) {
  console.error('Erro ao gerar PDF:', err.message);
  process.exit(1);
}
