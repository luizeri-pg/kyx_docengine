# Integração KYX Core ↔ DocEngine (geração de PDF)

Este documento é o contrato **implementado no código** para o time **KYX Core**. Alguns PDFs de especificação antigos mostram `POST /documents/generate` devolvendo o **base64 do PDF na mesma resposta**; **não é o comportamento atual**.

## Fluxo assíncrono (implementado)

1. **Autenticação** — `POST /auth/login` com `username` e `password` no body. Resposta padrão `ApiResponse` com `resultado.access_token` (Bearer).
2. **Disparar geração** — `POST /documents/generate` com header `Authorization: Bearer <token>`.
3. **Resposta do POST** — devolve **`jobId`** (GUID) e **`status: queued`** (ou similar). **Não** contém o PDF em base64.
4. **Obter o PDF** — `GET /documents/status/{jobId}` até `resultado.status === "completed"`. Então `resultado.resultado` contém o objeto com **`base64`**, `contentType` (ex.: `application/pdf`) e `nomeArquivo`.

O processamento roda na **fila Hangfire** (`DocumentWorker`); o tempo depende do template (HTML + Puppeteer ou AcroForm + PdfSharp).

### Exemplo de sequência (resumido)

```http
POST /auth/login
Content-Type: application/json

{ "username": "...", "password": "..." }
```

```http
POST /documents/generate
Authorization: Bearer <token>
Content-Type: application/json

{
  "requisicaoId": "uuid-da-correlação",
  "config": {
    "template": "slug-do-template",
    "centroCusto": "identificador-monitoramento",
    "nomeArquivo": "documento.pdf"
  },
  "dados": {
    "nome": "JOAO DA SILVA",
    "cpf": "00000000000"
  }
}
```

### Template inline (sem gravar na tabela `templates`)

Em alternativa a `config.template` (slug), pode enviar **`config.inlineTemplate`**: o HTML ou PDF em base64 (AcroForm) vai só no pedido e **não** é persistido na tabela `templates`. O job continua registado em `document_jobs` (com snapshot JSON para o worker). **Não** use `template` e `inlineTemplate` no mesmo pedido.

```json
{
  "requisicaoId": "corr-inline-001",
  "config": {
    "centroCusto": "DEV001",
    "nomeArquivo": "doc.pdf",
    "inlineTemplate": {
      "type": "html",
      "content": "<!DOCTYPE html><html><body><p>Olá, {{nome}}</p></body></html>",
      "requiredFields": ["nome"]
    }
  },
  "dados": {
    "nome": "Maria"
  }
}
```

Requer migração que torna `document_jobs.template_id` opcional e adiciona `template_snapshot_json` (ver migração `DocumentJobInlineTemplateSnapshot`).

**Resposta típica do POST (200):**

```json
{
  "sucesso": true,
  "requisicaoId": "uuid-da-correlação",
  "tempoProcessamento": 50,
  "resultado": {
    "jobId": "guid-do-job",
    "status": "queued"
  }
}
```

**Polling:**

```http
GET /documents/status/{jobId}
Authorization: Bearer <token>
```

Quando o job concluir com sucesso, o corpo da resposta segue o formato `ApiResponse` (camelCase). O **PDF** fica no objeto **`resultado` aninhado** (o `DocumentStatusResponse` também expõe uma propriedade `resultado` com o binário):

```json
{
  "sucesso": true,
  "requisicaoId": "uuid-da-requisição",
  "tempoProcessamento": 120,
  "resultado": {
    "jobId": "guid-do-job",
    "status": "completed",
    "errorMessage": null,
    "resultado": {
      "base64": "JVBERi0xLjQK...",
      "contentType": "application/pdf",
      "nomeArquivo": "documento.pdf"
    }
  }
}
```

Se `resultado.status === "failed"`, usar `resultado.errorMessage`; o PDF não virá preenchido.

## Divergência vs. especificação PDF antiga

| Tópico | Especificação antiga (exemplo) | Código atual |
|--------|-------------------------------|--------------|
| Resposta do `POST /documents/generate` | `resultado.base64` do PDF na hora | `resultado.jobId` + fila; base64 só no `GET /status` |
| Login | Exemplo com JWT corporativo longo | DocEngine local: `admin` / `admin123` (ajustar por ambiente); formato `ApiResponse` igual |

## Onde está no código

- Controller: `KYX.DocEngine.API/DocEngine/Controllers/DocumentsController.cs` (`POST generate`, `GET status`)
- Worker: `DocEngine/Workers/DocumentWorker.cs`
- Motor PDF: `DocEngine/Services/PdfEngineService.cs`

## Segurança em rede

Restrição por **VPN** ou rede privada é **política de infraestrutura**, não regra aplicada dentro da API.

Para detalhes de **templates** (HTML vs AcroForm), ver [TEMPLATES-PLAYBOOK.md](./TEMPLATES-PLAYBOOK.md).
