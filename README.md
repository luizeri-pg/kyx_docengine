# KYX DocEngine

Plataforma para **gestão de templates** e **geração de PDF** (HTML e AcroForm) com processamento assíncrono.

## Documentação (SDD)

Especificação em [`SDD/`](SDD/) — escopo: **geração de PDF** (templates HTML/AcroForm, fila, auditoria, centro de custo). **Não** há envio de e-mail nem notificações no produto atual.

### Integração e templates (operacional)

| Documento | Público |
|-----------|---------|
| [**Especificação KYX DocEngine (consolidada)**](docs/ESPECIFICACAO-KYX-DOCENGINE.md) | Objetivo, escopo, segurança, exemplos de login/generate e **divergência** spec PDF síncrona vs API assíncrona. |
| [**Integração KYX Core**](docs/INTEGRACAO-KYX-CORE.md) | Contrato real: `POST /documents/generate` é **assíncrono**; PDF em base64 via **`GET /documents/status/{jobId}`** (diferente de especificações PDF antigas com resposta síncrona). |
| [**Playbook de templates**](docs/TEMPLATES-PLAYBOOK.md) | HTML `{{variáveis}}` vs AcroForm, `requiredFields`, `POST /templates/inspect-pdf`. |
| [**Teste PDF local**](docs/TESTE-PDF-LOCAL.md) | Template demo `demo_pdf_local` (Development) + curl/Swagger para gerar PDF com dados. |
| [**Template Fidelizza (ficha completa)**](docs/TEMPLATE-FIDELIZZA.md) | Slug `template-fidelizza` + `POST /documents/generate` só com `config` + `dados` (sem inline). |
| [**Book Admissional Fidelizza 2025**](docs/TEMPLATE-BOOK-ADMISSIONAL-FIDELIZZA.md) | Template dinâmico multipágina + exemplo `POST /documents/generate` sem inline. |
| [**Banco de dados — tabelas**](docs/BANCO-DADOS-TABELAS.md) | Inventário: `templates`/`document_jobs` (PDF), todas as `tb_*` (plataforma KYX/Notify) e scripts SQL. |

## Stack

- Backend: .NET 10 + PostgreSQL + Hangfire (Redis ou memória em dev)
- Frontend: Vite + React + TypeScript — telas: **Templates**, **Gerar documento**, **Jobs**

## Início rápido

Para **subir a API e testar PDF via API** (sem obrigar alterações na BD), ver **[docs/SETUP-DO-ZERO.md](docs/SETUP-DO-ZERO.md)**.

### Backend

```bash
cd backend/KYX.DocEngine.API
dotnet restore
dotnet run
```

Backend em `http://localhost:3000`.

### Frontend

```bash
cd frontend
npm install
npm run dev
```

Frontend em `http://localhost:5173`.

**O front fala com a API em `http://localhost:3000`.** Se aparecer `ERR_CONNECTION_REFUSED` no browser, inicie o backend antes (`dotnet run` na pasta da API). Opcional: copie `frontend/.env.example` para `frontend/.env` e defina `VITE_API_URL` se a API estiver noutro host/porta.

## API principal

- `POST /auth/login`
- `POST /documents/generate`
- `POST /documents/generate-sync` (PDF síncrono sem `document_jobs`; opcional, ver [`docs/TESTE-PDF-LOCAL.md`](docs/TESTE-PDF-LOCAL.md))
- `GET /documents/status/{jobId}`
- `GET/POST/PUT/DELETE /templates`
- `POST /templates/inspect-pdf`

## Banco e fila

- **PostgreSQL:** connection string em `appsettings.json` ou `appsettings.Local.json` (ver [`backend/README.md`](backend/README.md)).
- **Migrações no arranque:** por defeito **desligadas** (`Database:ApplyMigrationsOnStartup: false`) — a API **não cria tabelas** ao iniciar; use `dotnet ef database update` ou scripts DBA quando quiser aplicar o schema.
- **Hangfire:** em Development o padrão é **memória** (`appsettings.Development.json`); produção com Redis — ver backend README.

Para criar/atualizar o schema (quando tiver permissão no PostgreSQL), rode:

```bash
dotnet ef migrations add InitialCreate --project backend/KYX.DocEngine.API/KYX.DocEngine.API.csproj
dotnet ef database update --project backend/KYX.DocEngine.API/KYX.DocEngine.API.csproj
```

