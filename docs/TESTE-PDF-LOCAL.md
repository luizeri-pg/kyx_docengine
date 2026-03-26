# Teste local: template + PDF com dados

Fluxo ponta a ponta: autenticar → (opcional) criar template → enfileirar geração → obter PDF em base64.

## Pré-requisitos

- API em execução (`dotnet run` na pasta `backend/KYX.DocEngine.API` ou conforme `backend/README.md`).
- PostgreSQL acessível e migrações aplicadas (`Database:ApplyMigrationsOnStartup` ou `dotnet ef database update`).
- Hangfire com storage configurado (em **Development**, `Hangfire:Storage` = `Memory` costuma bastar).
- Migração **`DocumentJobInlineTemplateSnapshot`** aplicada se for usar **`config.inlineTemplate`** (ou script SQL em [`sql/alter-document_jobs-inline-template.sql`](./sql/alter-document_jobs-inline-template.sql)).

## PDF **sem** tabela `document_jobs` (síncrono)

Com ambiente **Development** e `Documents:AllowSyncPdfGeneration` = true (já em `appsettings.Development.json`):

**`POST /documents/generate-sync`** — gera o PDF na mesma resposta (`resultado.base64`), **sem** fila Hangfire e **sem** `INSERT` em `document_jobs`. Continua a exigir **JWT** (`Authorization: Bearer`), ou seja, login ainda usa `tb_usuario` salvo na configuração habitual — só o passo de PDF deixa de depender do schema de jobs.

```bash
curl -s -X POST http://localhost:3000/documents/generate-sync \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "requisicaoId": "sync-001",
    "nomeArquivo": "teste.pdf",
    "inlineTemplate": {
      "type": "html",
      "content": "<!DOCTYPE html><html><body><p>Olá {{nome}}</p></body></html>",
      "requiredFields": ["nome"]
    },
    "dados": { "nome": "Teste" }
  }' | python3 -m json.tool
```

Em **Production**, o endpoint responde **403** se `AllowSyncPdfGeneration` estiver false.

---

## Teste mais rápido: PDF sem tabela `templates` (inline)

Na raiz do repositório:

```bash
chmod +x scripts/test-pdf-inline.sh
BASE=http://localhost:3000 ./scripts/test-pdf-inline.sh
```

Gera PDF só com `inlineTemplate` no JSON (ver [INTEGRACAO-KYX-CORE.md](./INTEGRACAO-KYX-CORE.md)). O ficheiro sai como `./teste-docengine.pdf` (ou defina `OUT=/caminho/ficheiro.pdf`).

**Importante:** recompile e reinicie a API após atualizar o código (`dotnet build` + `dotnet run`). Se aparecer erro de validação *"The Template field is required"*, o processo em execução ainda é **binário antigo**.

## Template de demonstração (Development)

Com a API em modo **Development**, após migrações bem-sucedidas é criado automaticamente um template HTML se ainda não existir:

| Campo | Valor |
|--------|--------|
| **Slug** | `demo_pdf_local` |
| **Tipo** | `html` |
| **Placeholders** | `{{nome}}`, `{{cpf}}`, `{{dataDoc}}` |

Se preferir criar manualmente, use `POST /templates` com `type: "html"` e `content` contendo as mesmas chaves (ver [TEMPLATES-PLAYBOOK.md](./TEMPLATES-PLAYBOOK.md)).

## Contrato `POST /documents/generate`

O body segue `GenerateDocumentRequest` (camelCase no JSON):

| Campo | Descrição |
|--------|-----------|
| `requisicaoId` | ID de correlação (string, obrigatório). |
| `config.template` | Slug do template na BD (ex.: `demo_pdf_local`), **ou** omita e use `inlineTemplate`. |
| `config.inlineTemplate` | Opcional: `{ "type": "html", "content": "...", "requiredFields": [] }` — não grava em `templates`. |
| `config.centroCusto` | Centro de custo (obrigatório na API). |
| `config.nomeArquivo` | Nome do PDF (opcional; default `documento.pdf`). |
| `dados` | Dicionário chave → valor (strings) para os placeholders do HTML. |

## Passos (curl)

Ajuste `BASE` e credenciais conforme seu `appsettings`.

```bash
BASE=http://localhost:3000
USER=admin@kyx.com.br
PASS='sua_senha'

# 1) Token (resposta: resultado.access_token)
TOKEN=$(curl -s -X POST "$BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"$USER\",\"password\":\"$PASS\"}" \
  | jq -r '.resultado.access_token // empty')
echo "Token: ${TOKEN:0:20}..."

# 2) Gerar documento (job assíncrono)
JOB_ID=$(curl -s -X POST "$BASE/documents/generate" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "requisicaoId": "test-local-001",
    "config": {
      "template": "demo_pdf_local",
      "centroCusto": "DEV001",
      "nomeArquivo": "demo.pdf"
    },
    "dados": {
      "nome": "Maria Silva",
      "cpf": "123.456.789-00",
      "dataDoc": "2026-03-23"
    }
  }' | jq -r '.resultado.jobId // empty')
echo "jobId: $JOB_ID"

# 3) Polling até completed (ajuste tentativas/sleep se necessário)
for i in $(seq 1 30); do
  STATUS=$(curl -s "$BASE/documents/status/$JOB_ID" \
    -H "Authorization: Bearer $TOKEN")
  echo "$STATUS" | jq .
  S=$(echo "$STATUS" | jq -r '.resultado.status // empty')
  if [ "$S" = "completed" ] || [ "$S" = "failed" ]; then
    break
  fi
  sleep 1
done

# 4) PDF em base64: ApiResponse envolve DocumentStatusResponse; o PDF fica em resultado.resultado.base64
echo "$STATUS" | jq -r '.resultado.resultado.base64 // empty' | head -c 80
echo "... (base64 truncado)"
```

Para gravar o PDF em ficheiro (quando `completed`):

```bash
echo "$STATUS" | jq -r '.resultado.resultado.base64' | base64 -d > demo.pdf
open demo.pdf   # macOS
```

## Swagger

1. `POST /auth/login` com `username` e `password` → copiar `resultado.access_token`.  
2. Authorize com `Bearer <access_token>`.  
3. `POST /documents/generate` com `requisicaoId`, `config` (template, centroCusto, nomeArquivo) e `dados`.  
4. `GET /documents/status/{jobId}` até `resultado.status` = `completed`; o base64 está em `resultado.resultado.base64`.

## Problemas comuns

| Sintoma | Causa provável |
|---------|----------------|
| **HTTP 500** em `POST /documents/generate` (template inline) | Na BD remota falta a migração **`DocumentJobInlineTemplateSnapshot`**: coluna `template_snapshot_json` e `template_id` nullable em `document_jobs`. Rode `dotnet ef database update` com a mesma connection string da API **ou** o SQL em [`sql/alter-document_jobs-inline-template.sql`](./sql/alter-document_jobs-inline-template.sql). Depois de recompilar a API, a resposta 500 inclui `mensagem` com o erro PostgreSQL (ex.: *column ... does not exist*). |
| Job fica `pending` / não completa | Hangfire não está a processar (Redis indisponível, worker parado). |
| Erro ao migrar / sem tabelas | Permissões no PostgreSQL ou migrar com utilizador adequado. |
| Template não encontrado | Slug errado ou seed não correu (não é Development ou migração falhou). |
