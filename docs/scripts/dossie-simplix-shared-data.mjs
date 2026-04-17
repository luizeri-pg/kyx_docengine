/**
 * Dados compartilhados entre o mock (HTML manual) e o JSON do template `template-dossie-simplix.html`.
 * Copiados do extrato / mock alinhado ao PDF original (Caroline / Simplix).
 */
export const HASH = '0d7262794e2af4880398432552e94f098d0c2bfff17342eadfa6ea01294c3d15';
export const stdIp = '189.76.168.193:60992';
export const stdLat = '-23.4188809';
export const stdLon = '-51.9370214';
export const stdUa =
  'Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Mobile Safari/537.36';

export const allEvents = [
  ['Transação Reiniciada', '18/12/2025<br>13:29:43 (-03:00)'],
  ['Saudação', '18/12/2025<br>13:29:43 (-03:00)'],
  ['Aceite documental (ref. anexos PDF)', '18/12/2025<br>13:29:43 (-03:00)'],
  ['Captura De Documento', '18/12/2025<br>13:29:43 (-03:00)'],
  ['Documento Aprovado', '18/12/2025<br>13:32:31 (-03:00)'],
  ['Processamento Dados', '18/12/2025<br>13:32:31 (-03:00)'],
  ['Dados Aprovado', '18/12/2025<br>13:32:46 (-03:00)'],
  ['Processamento Ccb', '18/12/2025<br>13:32:46 (-03:00)'],
  ['Opt-In Ccb', '18/12/2025<br>13:32:55 (-03:00)'],
  ['Captura Liveness', '18/12/2025<br>13:33:13 (-03:00)'],
  ['Processamento Face', '18/12/2025<br>13:33:41 (-03:00)'],
  ['Face Aprovada', '18/12/2025<br>13:33:47 (-03:00)'],
  ['Processamento Facematch', '18/12/2025<br>13:33:47 (-03:00)'],
  ['Facematch Aprovado', '18/12/2025<br>13:33:52 (-03:00)'],
  ['Processamento Dossiê', '18/12/2025<br>13:33:52 (-03:00)']
];

export function fmt(txt) {
  return txt.replace(/\n/g, '<br>').replace(/\*([^*]+)\*/g, '<b>$1</b>');
}

export const allInteractions = [
  [
    '18/12/2025<br>13:29:43',
    '',
    fmt(`Olá! Seja bem-vindo(a), Caroline!\n\nVamos iniciar a jornada de formalização da sua antecipação de saque aniversário do FGTS.\n\nPara iniciarmos, clique em começar. Eu vou precisar que você autorize o acesso à sua câmera e à sua localização. Quando aparecer a mensagem solicitando autorização, clique em permitir.\n\nÓtimo! Vamos avançar. Para sua segurança, envie um documento de identificação com foto. O processo é bem simples, e estarei aqui para ajudar você. *Atenção:* ao fotografar seu documento, use a câmera do celular em um ambiente bem iluminado. Tenha cuidado com reflexos e certifique-se de que a imagem esteja clara e que todo o documento esteja legível.\n\n*Termos de Uso e Política de Privacidade*\nPara darmos continuidade à formalização digital, estou enviando nossos Termos de Uso, nossa Política de Privacidade. Leia atentamente e veja também as informações abaixo:\n1. Autorizo a Simplix a informar e consultar meus dados pessoais ao/no Sistema de Informações de Crédito (SCR) do Banco Central do Brasil (Bacen), conforme disposto na Resolução CMN nº 4.571 de 26 de maio de 2017.\n2. Aceito os Termos de Uso, nossa Política de Privacidade.\nContrate com responsabilidade.\n\nArquivo: Política de Privacidade\nCheckbox: Estou de acordo com todas as condições da contratação.\nComeçar`)
  ],
  ['18/12/2025<br>13:29:51', 'Cliente', 'CONTINUAR'],
  ['18/12/2025<br>13:29:51', '', 'Escolha um tipo de documento para ser capturado:'],
  ['18/12/2025<br>13:30:00', 'Cliente', 'CONTINUAR_CNH'],
  ['18/12/2025<br>13:30:00', '', 'Escolha como deseja fazer o upload do documento:'],
  [
    '18/12/2025<br>13:32:55',
    '',
    fmt(`Muito obrigado, *CAROLINE*! Estamos quase finalizando a contratação da sua antecipação de saque aniversário do FGTS.\n\nAqui estão as principais condições do seu empréstimo:\n\nProposta: 143625\n\nValor a receber: R$ 195.53\n\nValor da operação: R$ 266.18\n\nTaxa de juros: 1.6999 % a.m. / 22.42 % a.a.\n\nCusto Efetivo Total (CET): 67.17 % a.a.\n\nPrazo do contrato: 2 anos\n\nPrimeiro vencimento: 01/09/2026\n\nÚltimo vencimento: 01/09/2027\n\nDados para liberação:\n\nMeio de Pagamento: Transferência\nBanco: 237\nAgência: 36-\nConta: 40213-3\n\nArquivo: CONTRATO Proposta: 143625\nCheckbox: Estou de acordo com todas as condições da contratação.\nContinuar`)
  ],
  ['18/12/2025<br>13:33:06', 'Cliente', 'Checkbox: true'],
  ['18/12/2025<br>13:33:06', 'Cliente', 'CONTINUAR'],
  [
    '18/12/2025<br>13:33:06',
    '',
    fmt(`Tenho ótimas notícias! Chegou a hora de assinar seu(s) contrato(s) de empréstimo!\n\nAgora, preciso que você tire uma selfie, que será usada como sua assinatura eletrônica no contrato. Por isso, é importante que você tenha lido todos os documentos enviados durante este processo, ok?\n\nVamos começar? Siga as orientações abaixo:\n\n1 - Esteja em um local bem iluminado.\n\n2 - Retire quaisquer acessórios do rosto, como boné, óculos ou máscara.\n\n3 - Posicione o celular na altura do rosto.\n\n4 - Encaixe seu rosto no local indicado.\nAntes de prosseguir, é importante que você leia e concorde com os termos abaixo:\n\nBem-vindo(a) aos Termos de Uso do UNICO | CHECK\n\nSe você está aqui, significa que a empresa que você escolheu para fazer uma transação nos contratou para garantir que a sua identidade seja preservada e a privacidade dos seus dados seja respeitada. Somos a UNICO, uma IDtech brasileira com soluções para simplificar a vida das pessoas por meio de sua identidade digital.\n\nVocê pode saber mais como tratamos seus dados pessoais clicando aqui: https://unico.io/juridico/unicopoliticadeprivacidade.pdf\n\nSEUS DADOS, SUAS REGRAS!\n\nAgora que você já nos conheceu um pouquinho e entendeu como tratamos os seus dados pessoais, mas ainda assim sentiu falta de alguma informação, sintase à vontade para entrar em contato conosco por aqui: https://unico.io/privacidade-e-gestao-de-dados/\n\nContinuar`)
  ],
  ['18/12/2025<br>13:33:13', 'Cliente', 'CONTINUAR'],
  [
    '18/12/2025<br>13:33:13',
    '',
    fmt(`Vamos lá, prepare-se para começar\n\nEnquadre seu rosto na marcação, pressione o botão abaixo e siga as indicações na tela.\n\nEstou pronto`)
  ],
  ['18/12/2025<br>13:33:41', '', 'Aguarde...'],
  [
    '18/12/2025<br>13:33:52',
    '',
    fmt(`*O processo foi concluído com sucesso!*\n\nVeja, abaixo, as informações acerca da sua solicitação da sua antecipação de saque aniversário do FGTS.\n\nProposta: 143625\n\nValor a receber: R$ 195.53\n\nValor da operação: R$ 266.18\n\nTaxa de juros: 1.6999 % a.m. / 22.42 % a.a.\n\nCusto Efetivo Total (CET): 67.17 % a.a.\n\nPrazo do contrato: 2 anos\n\nPrimeiro vencimento: 01/09/2026\n\nÚltimo vencimento: 01/09/2027\n\nDados para liberação:\n\nMeio de Pagamento: Transferência\nBanco: 237\nAgência: 36-\nConta: 40213-3\n\nA Simplix está à sua disposição. Para mais informações, acesse nosso site: www.simplix.com.br.`)
  ]
];

export const termosP1 = `<p>Estes Termos de Uso e Política de Privacidade ("Termos") regulamentam o acesso e uso dos portais, sites e aplicativos de serviços bancários e financeiros ("Plataformas") da Simplix, compreendendo Simplix e demais empresas relacionadas.</p>
<h3>1. Aceitação e Acesso</h3><p>Ao acessar as Plataformas (disponíveis via web ou nas lojas App Store e Play Store), o Usuário aceita integralmente estes Termos. Caso discorde de qualquer disposição, deverá interromper o uso das Plataformas. As informações fornecidas pelo Usuário são de sua exclusiva responsabilidade, sendo ele o único responsável por dados incorretos ou incompletos.</p>
<h3>2. Acesso e Segurança</h3><p>O acesso às Plataformas poderá ser realizado mediante login, senha, token ou biometria (Touch ID/Fingerprint), estando disponível para aparelhos compatíveis. Cabe ao Usuário desativar essa função caso deseje desabilitar o acesso via biometria. As informações biométricas são armazenadas pelo fabricante do dispositivo, sendo acessíveis a Simplix apenas se fornecidas diretamente pelo Usuário.</p>
<h3>3. Responsabilidades do Usuário</h3><p>O Usuário declara que:</p><ul><li>É civilmente capaz e penalmente imputável;</li><li>Utilizará as Plataformas exclusivamente para fins lícitos;</li><li>Não compartilhará conteúdo ilícito, ofensivo ou que viole direitos de terceiros, incluindo materiais que contenham vírus ou outras ameaças à segurança.</li></ul>
<h3>4. Propriedade Intelectual</h3><p>Todo o conteúdo das Plataformas, incluindo softwares, marcas e logomarcas, pertence a Simplix e está protegido por leis de propriedade intelectual. A cópia, reprodução ou uso para fins não autorizados sujeitam o Usuário a penalidades legais.</p>
<h3>5. Privacidade e Política de Cookies</h3><p>Para prestar seus serviços, a Simplix coleta dados pessoais e informações do dispositivo, como dados de contato, IP, geolocalização e características do navegador. Além disso, são utilizados cookies para otimizar a navegação, que podem ser desativados no navegador do Usuário, mas podem limitar funcionalidades.</p>`;

export const termosP2 = `<p>A tabela abaixo apresenta os dados pessoais coletados e suas finalidades:</p>
<table><thead><tr><th>Dados Pessoais Coletados</th><th>Finalidades</th></tr></thead><tbody>
<tr><td>Razão Social, CNPJ, Endereço, Telefone, E-mail, Dados Bancários.</td><td>Cadastro de Correspondentes na Plataforma.</td></tr>
<tr><td>Nome, CPF</td><td>Cadastro de Usuários na Plataforma.</td></tr>
<tr><td>Nome, CPF, RG, Data de Nascimento, Nacionalidade, Telefone, Estado Civil, E-mail, Ocupação, Endereço, Dados Bancários, Dados Biométricos.</td><td>Cadastro de clientes na Plataforma.</td></tr></tbody></table>
<p>A esses Dados Pessoais somente terão acesso integrantes da Simplix e terceiros diretamente envolvidos com a execução dos serviços.</p>
<p>Os dados biométricos, como imagem facial, digitais e voz, poderão ser utilizados para proteção e autenticação do Usuário, respeitando a privacidade e segurança.</p>
<p>Dados pessoais poderão ser compartilhados com terceiros em casos como:</p><ul><li>Para viabilizar serviços contratados;</li><li>Em conflitos, mediante ordem judicial;</li><li>Em operações societárias ou por exigência legal.</li></ul>
<p>A Simplix, portanto, se compromete a buscar parceiros que respeitem normas de privacidade e proteção de dados pessoais.</p>
<h3>6. Segurança</h3><p>O Usuário deve adotar medidas de segurança adequadas, como não compartilhar senhas e manter antivírus e firewall atualizados. A Simplix não solicita informações pessoais ou credenciais via e-mail.</p>`;

export const termosP3 = `<h3>7. Disponibilidade e Limitação de Responsabilidade</h3><p>A Simplix poderá suspender, cancelar ou limitar o acesso às Plataformas sem aviso prévio, caso identifique violação destes Termos. A Simplix não se responsabiliza por:</p><ul><li>Indisponibilidade das Plataformas ou falhas de acesso;</li><li>Dificuldades de conexão e qualidade do sinal de internet;</li><li>Acesso de terceiros não autorizados por falha do Usuário.</li></ul>
<h3>8. Contato</h3><p>As comunicações entre Usuário e a Simplix devem ser realizadas pela Central de Atendimento ao Cliente ou Serviço de Atendimento ao Cliente (SAC). A Simplix poderá entrar em contato via e-mail, telefone ou outros meios informados pelo Usuário.</p>
<h3>9. Disposições Finais</h3><p>Este documento é regido pela legislação brasileira. Caso uma disposição seja considerada inválida, as demais permanecerão vigentes. Fica eleito o foro da Comarca da Cidade de São Paulo – SP para resolver litígios decorrentes destes Termos.</p>`;

export const ccbSections = [
  `<h3>1. DA CONCESSÃO DO CRÉDITO</h3><p>1.1. Nos termos da Lei 10.931/2004 e legislação acessória vigente e regulada pelas cláusulas e condições abaixo constantes, declara o EMITENTE, já devidamente qualificado, que na hipótese em que o Agente Operador do FGTS não transferir o valor devido ao CREDOR ou a eventual sucessor, por qualquer motivo que seja, conforme o fluxo estabelecido na cláusula "VII – FLUXO DE PAGAMENTOS" acima, o EMITENTE reconhece que deverá pagar a totalidade, ou valor remanescente da parcela por meio desta cédula de crédito bancário ("CCB") ao CREDOR ou a seu eventual sucessor, em moeda corrente nacional, a quantia líquida, certa e exigível, acrescida dos juros à taxa indicada nesta CCB.</p><p>1.2. O EMITENTE e eventual(ais) GARANTIDOR(es) declara(m) e garante(m) que está(ão) devidamente autorizado(s) a firmar a presente CCB.</p><p>1.3. Da Natureza do Empréstimo: O EMITENTE declara ciência que, em razão de sua natureza, a presente operação de empréstimo demanda a intermediação de informações entre o CREDOR e o Agente Operador do FGTS.</p><p>1.4. Tarifa de Cadastro: O EMITENTE ratifica sua ciência e anuência, de que a Tarifa de Cadastro tem seu valor cobrado de forma percentual sobre o valor da operação de empréstimo.</p><p>1.6. O EMITENTE tem expresso conhecimento de que os juros ajustados para o empréstimo são calculados de forma diária e capitalizada.</p><p>1.7. O EMITENTE declara que tomou conhecimento do cálculo do CET.</p><p>1.8. O EMITENTE se obriga a efetuar o pagamento do valor principal, acrescido dos encargos incidentes.</p><p>1.9. O EMITENTE declara e garante que cumpre o disposto na legislação referente à Política Nacional de Meio Ambiente.</p>`,

  `<h3>2. DA(S) GARANTIA(S)</h3><p>2.1. Em garantia do cumprimento integral de todas as obrigações, principais e acessórias, presentes e futuras, decorrentes da emissão desta CCB ("Obrigações Garantidas"), o EMITENTE cede ao CREDOR, em caráter irrevogável e irretratável, a propriedade fiduciária dos Direitos de Saque Aniversário, nos termos do artigo 66-B da Lei nº 4.728/65 e do artigo 20-D, §3º, da Lei nº 8.036/90.</p><p>2.2. Redistribuição da(s) Parcela(s): Se houver a redução dos Direitos de Saque Aniversário bloqueados ou disponíveis, o CREDOR fica autorizado pelo EMITENTE a aumentar o valor bloqueado ou redistribuir o valor e vencimento da(s) Parcela(s).</p><p>2.3. Execução Antecipada da Garantia: Se houver movimentação da conta vinculada do EMITENTE no FGTS que ocasione a redução do valor bloqueado, o CREDOR poderá executar antecipadamente a garantia.</p><p>2.4. Autorizações Concedidas: O EMITENTE autoriza o CREDOR a realizar o bloqueio dos seus Direitos de Saque Aniversário junto ao Agente Operador do FGTS.</p><p>2.5. O EMITENTE declara sua ciência de que os valores bloqueados ficarão indisponíveis para saque diretamente pelo EMITENTE.</p><h3>3. DO ATRASO NO PAGAMENTO E ENCARGOS MORATÓRIOS</h3><p>3.1. Na hipótese de inadimplemento ou mora: juros remuneratórios, juros de mora de 1% a.m., multa de 2%, despesas de cobrança limitadas a 10%.</p><h3>4. DO VENCIMENTO ANTECIPADO</h3><p>4.1. O empréstimo poderá ser integralmente exigido pelo CREDOR, por vencimento antecipado automático.</p>`,

  `<h3>5. LIQUIDAÇÃO ANTECIPADA</h3><p>5.1. O EMITENTE poderá, a qualquer tempo, liquidar antecipadamente, total ou parcialmente, suas obrigações decorrentes desta CCB, mediante requerimento enviado ao CREDOR com antecedência de 05(cinco) dias.</p><h3>6. DAS DECLARAÇÕES</h3><p>6.1. O EMITENTE e eventuais GARANTIDORES declaram e garantem que possuem plena capacidade e legitimidade para celebrar a presente CCB, realizar todas as operações e cumprir todas as obrigações assumidas.</p>`,

  `<h3>7. DISPOSIÇÕES FINAIS</h3><p>7.1. A apuração do saldo devedor será realizada pelo CREDOR mediante planilha de cálculo.</p><p>7.3. Tolerância: A tolerância não implica perdão, renúncia, novação ou alteração da dívida.</p><p>7.5. Comunicação aos Serviços de Proteção ao Crédito.</p><p>7.7. Aval e Solidariedade.</p><p>7.10. Consultas e atualização de dados cadastrais e de crédito.</p><p>7.11. Comunicação ao Sistema de Informação de Créditos ("SCR").</p><p>7.12. Tratamento de Dados Pessoais.</p><p>7.13. LGPD: O CREDOR compromete-se a tratar os Dados Pessoais de acordo com a Lei nº 13.709/2018.</p><p>7.14. Efeitos da CCB.</p><p>7.15. Irrevogabilidade e Irretratabilidade.</p><p>7.18. Legislação: Aplica-se à presente CCB as disposições da Lei 10.931, de 02 de agosto de 2004.</p><p>7.20. Cessão ou Endosso: O CREDOR fica expressamente autorizado a ceder a terceiros os direitos de crédito.</p><p>7.22. Emissão de Certificados de CCB.</p><p>7.24. Formas de assinatura: A presente CCB será emitida e assinada de forma eletrônica.</p><p>7.25. Foro: Comarca de São Paulo/SP.</p><p>7.26. A presente CCB é emitida e firmada em 2 (DUAS) vias.</p><p style="margin-top:14px;text-align:center;"><b>Local e data:</b> São Paulo, 17 de dezembro de 2025</p><p style="text-align:center;"><b>Assinaturas:</b></p>`
];
