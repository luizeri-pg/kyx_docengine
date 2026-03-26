# KYX DocEngine — Especificação (texto consolidado)

Versão em prosa corrigida a partir do documento de negócio; detalhes técnicos de contrato HTTP estão alinhados com o código e com [INTEGRACAO-KYX-CORE.md](./INTEGRACAO-KYX-CORE.md).

---

## Objetivo

Este serviço abstrai a geração de documentos e formulários, oferecendo **uma camada única e padronizada de comunicação** para o KYX Core.

Independente do layout do PDF — contrato, termo, ficha ou outro tipo — o Core envia apenas os **dados necessários**; o DocEngine processa, preenche e devolve o documento final em **PDF pronto para uso** (na prática, **base64** para consumo pelo Core).

---

## Escopo

O serviço atua como **camada centralizada** para processamento e preenchimento de formulários e documentos. Capacidades esperadas:

### Gestão de formulários e documentos

Permitir criação, edição e manutenção de qualquer tipo de formulário ou layout de documento.

### Importação de template em PDF e mapeamento de variáveis *(requisito de produto)*

É necessária uma funcionalidade que permita **importar um template em PDF**, **mapear as variáveis** existentes no documento e, a partir disso, o sistema **preencha automaticamente** os campos e gere o PDF final.

**Como isso se reflete hoje no DocEngine:**

| Necessidade | Implementação atual |
|-------------|---------------------|
| PDF com campos de formulário (AcroForm) | Template `type: acroform` — conteúdo = PDF em **base64**; chaves de `dados` = **nomes dos campos** do PDF. |
| Descobrir nomes dos campos no PDF | `POST /templates/inspect-pdf` com `pdfBase64` — lista campos para montar `requiredFields` e o contrato com o Core. |
| Layout rico sem AcroForm | Template `type: html` — HTML com `{{variáveis}}` e renderização para PDF. |

Fluxo de “importar PDF → mapear → gerar” na UI/admin pode evoluir; a **API** já suporta cadastro de template e geração com dados.

---

## Segurança

- **Autenticação** por **Bearer Token** (`Authorization: Bearer <access_token>`).
- **Acesso restrito** (ex.: exclusivamente via **VPN**) é **política de rede/infra**; a API não substitui firewall ou VPN.

---

## Controles e observabilidade

- **Centro de custo** — enviado em `config.centroCusto` no `POST /documents/generate` para monitoramento e gestão de uso por fornecedor/contexto.
- **Logs detalhados** — rastreabilidade de requisições e respostas (modelo alinhado ao padrão de log da plataforma, ex. `tb_log_requisicao` onde aplicável).

---

## Sistema — interface administrativa (direção)

Módulos desejados:

- Login com controle de acesso.
- **Usuários** — cadastro para integração ou front-end.
- **Consulta de logs** — auditoria e troubleshooting.
- **Configuração de templates** — criação, edição e manutenção de formulários/layouts.

*(O repositório pode incluir telas parciais; evoluir conforme roadmap.)*

---

## Integração — autenticação

### `POST /auth/login`

**Request (exemplo):**

```json
{
  "username": "safemais",
  "password": "s212@lj*ddfd2"
}
```

**Response (formato `ApiResponse`):**

```json
{
  "sucesso": true,
  "mensagem": null,
  "tempoProcessamento": 102,
  "requisicaoId": "48897d30-1cf9-42a8-ba00-def416dd3950",
  "resultado": {
    "expires_in": 3600,
    "access_token": "<JWT>",
    "token_type": "Bearer"
  }
}
```

Use `resultado.access_token` no header das demais rotas.

---

## Integração — processamento de documentos

### `POST /documents/generate`

**Request (exemplo — alinhado ao Core):**

```json
{
  "requisicaoId": "48897d30-1cf9-42a8-ba00-def416dd3950",
  "config": {
    "template": "fideliza_termo_genero",
    "centroCusto": "sfairalimentos",
    "nomeArquivo": "documento.pdf"
  },
  "dados": {
    "nome": "JOAO DA SILVA",
    "cpf": "31343242",
    "nameMae": "MARIA SILVA",
    "dataNascimento": "13/12/1991"
  }
}
```

### Divergência importante: resposta síncrona vs implementação

Alguns PDFs de especificação mostram o **`POST /documents/generate`** devolvendo **na mesma resposta** algo como:

```json
"resultado": {
  "base64": "REFTREFTRFNBREFTREFER08yWTgyOUlSUDQzS1JGOTgzNFVGMzBMUE8zNEYzNEY=",
  "contentType": "application/pdf",
  "nomeArquivo": "documento.pdf"
}
```

**No código atual isso não ocorre.** O fluxo é **assíncrono**:

1. O **POST** devolve **`jobId`** e status de fila (ex. `queued`).
2. O Core deve chamar **`GET /documents/status/{jobId}`** até `status === "completed"`.
3. O **base64** do PDF vem em **`resultado.resultado.base64`** (objeto aninhado; ver [INTEGRACAO-KYX-CORE.md](./INTEGRACAO-KYX-CORE.md)).

Motivo: processamento em fila (Hangfire), HTML→PDF ou AcroForm.

---

## Referências rápidas

| Documento | Conteúdo |
|-----------|-----------|
| [INTEGRACAO-KYX-CORE.md](./INTEGRACAO-KYX-CORE.md) | Contrato HTTP real (async, polling, caminhos JSON). |
| [TEMPLATES-PLAYBOOK.md](./TEMPLATES-PLAYBOOK.md) | HTML `{{vars}}`, AcroForm, `inspect-pdf`, `requiredFields`. |
| [TESTE-PDF-LOCAL.md](./TESTE-PDF-LOCAL.md) | Teste local com template demo. |
