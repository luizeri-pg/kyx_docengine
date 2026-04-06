# KYX DocEngine - Backend .NET 10

Backend do **DocEngine** responsável por gerenciar templates e gerar documentos PDF de forma assíncrona.

**Integração com o KYX Core:** o `POST /documents/generate` devolve `jobId` (fila Hangfire); o PDF em base64 vem no **`GET /documents/status/{jobId}`**. Ver [docs/INTEGRACAO-KYX-CORE.md](../docs/INTEGRACAO-KYX-CORE.md) e [docs/TEMPLATES-PLAYBOOK.md](../docs/TEMPLATES-PLAYBOOK.md).

**Tabelas PostgreSQL:** inventário completo (`templates`, `document_jobs`, `tb_*`) em [docs/BANCO-DADOS-TABELAS.md](../docs/BANCO-DADOS-TABELAS.md).

## Tecnologias

- ASP.NET Core Web API
- Entity Framework Core + PostgreSQL
- JWT Bearer Authentication
- Hangfire (memória por defeito; Redis opcional)
- PuppeteerSharp (HTML -> PDF)
- PdfSharp (AcroForm -> PDF)

## Estrutura

```
backend/
├── KYX.DocEngine.sln
└── KYX.DocEngine.API/
    ├── DocEngine/
    │   ├── Controllers/
    │   ├── Data/
    │   ├── Models/
    │   ├── Services/
    │   ├── Middleware/
    │   ├── Workers/
    │   └── Filters/
    ├── Migrations/
    ├── appsettings.json
    └── Program.cs
```

## Execução

```bash
cd backend/KYX.DocEngine.API
dotnet restore
dotnet run
```

Servidor: `http://localhost:3000`

## Endpoints

- `POST /auth/login`
- `POST /documents/generate` (use `config.template` **ou** `config.inlineTemplate` para não gravar na tabela `templates`)
- `POST /documents/generate-sync` — PDF **síncrono** sem gravar em `document_jobs` (só com `Documents:AllowSyncPdfGeneration`; por defeito **ligado** em `Development`) — útil para testar o motor sem migrações
- `GET /documents/status/{jobId}`
- `GET/POST/PUT/DELETE /templates`
- `POST /templates/inspect-pdf`
- `GET /health`
- `GET /hangfire`

## Hangfire (fila de jobs)

- **Por defeito (`appsettings.json` + `Program.cs`):** **`Memory`** — sem Redis; fila e jobs **não** persistem após restart da API; com **várias réplicas**, cada instância tem a sua fila local.
- **Redis (opcional):** `Hangfire:Storage` = `Redis` e `ConnectionStrings:Redis` (o código acrescenta `abortConnect=false` se faltar).

## Banco de dados (connection string)

- Padrão em `appsettings.json` (localhost).
- **Override local (recomendado):** copie `appsettings.Local.json.example` para `appsettings.Local.json` e ajuste **Host**, **porta**, **Database**, **Username** e **Password** para o teu PostgreSQL. **Não deixes** o texto literal `SEU_HOST` no `Host=` — o erro *«nodename nor servname provided, or not known»* significa hostname inválido ou placeholder não substituído. **Connection refused** em `127.0.0.1:XXXX` → nada a escutar nessa porta (PostgreSQL parado ou **porta errada**: o padrão local é **5432**; 5442 costuma ser servidor remoto/túnel). Esse arquivo **não deve ser commitado** (está no `.gitignore`).
- O app e o `dotnet ef` carregam `appsettings.Local.json` automaticamente.

### Login (`POST /auth/login`) — tabela **`tb_usuario`**

O modelo segue a tabela **já usada no ecossistema Notify/KYX** (`tb_usuario`), não uma tabela nova `usuarios`.

| Coluna (PostgreSQL) | Uso no login |
|---------------------|--------------|
| `email`, `nome` | O `username` do JSON pode coincidir com **email**, **nome** ou, em bases legadas, **`id_usuario`** como texto (ex.: `301`). |
| `senha` | Hash **BCrypt** (recomendado) ou **MD5 em hex (32 chars, UTF-8)** em bases legadas — ver `Auth:PasswordVerification`. |
| `perfil_id` | Obrigatório na tabela; deve existir em `tb_perfil` se houver FK na base. |
| `ativo` | Tem de ser `true` para autenticar. |

Se a tua `tb_usuario` for **legada** (ex.: `id_usuario`, `str_login`, `str_senha`, `bloqueado`, sem `perfil_id`), configura **`Schema:Usuario`** em `appsettings.Local.json` — ver [`docs/TROUBLESHOOTING-SCHEMA.md`](../docs/TROUBLESHOOTING-SCHEMA.md) (secção *tb_usuario legada*).

**401 «Credenciais inválidas»** com `docengine.demo` / `DocEngine@2025` após o `INSERT`: em **Development**, o mapeamento legado (`str_login`, `id_usuario`, `bloqueado`, etc.) está em **`appsettings.Development.json`**. Confirme `ASPNETCORE_ENVIRONMENT=Development` (padrão do `dotnet run`). Ajuste **`appsettings.Local.json`** só com a **connection string** para a BD onde correu o `INSERT` (ficheiro opcional; ver `.example`).

- **`Auth:UseDatabaseForLogin`** (predefinição `true`): autenticação **só** contra **`tb_usuario`** (API/backend).
- **`Auth:UseAllowedLoginsFallback`**: se `false` (predefinição), **não** há utilizadores em ficheiro de configuração — credenciais vêm **apenas** da BD. Só ative `true` em desenvolvimento se precisares de login sem BD (lista em `Auth:AllowedLogins`).
- Migração **`MapUsuarioToTbUsuario`**: SQL **idempotente** (`CREATE TABLE IF NOT EXISTS tb_usuario` + índice único em `email`) — não rebenta se a tabela já existir. Script de referência: [`docs/sql/tb_usuario.sql`](../docs/sql/tb_usuario.sql).

#### Criar utilizador diretamente na BD

1. Gerar **hash BCrypt** da senha (mesma biblioteca que a API):
   ```bash
   cd backend/tools/HashPassword
   dotnet run -- "SuaSenhaSegura"
   ```
2. Colar o hash no script SQL e executar no PostgreSQL: [`docs/sql/insert-usuario-exemplo.sql`](../docs/sql/insert-usuario-exemplo.sql) (garante um `perfil_id` válido em `tb_perfil`).

### Não criar tabelas no arranque da API

Por defeito, **`Database:ApplyMigrationsOnStartup`** está **`false`**: o processo **não** executa `Database.Migrate()` ao iniciar — **não cria nem altera tabelas automaticamente**.

- Para **aplicar o schema** (uma vez, com utilizador que tenha permissão): `dotnet ef database update` na pasta do projeto, **ou** scripts SQL fornecidos pelo DBA.
- Se quiseres o comportamento antigo (migrações ao subir a API), define `"Database": { "ApplyMigrationsOnStartup": true }` em `appsettings` / `appsettings.Local.json` / variável de ambiente.

**Nota:** Mesmo sem migrar no arranque, a aplicação **continua a precisar das tabelas** (`templates`, `document_jobs`, `tb_log_requisicao`, etc.) para login persistido, templates e fila de PDF. Sem schema, essas operações falham.

## Migrações (CLI)

```bash
cd backend/KYX.DocEngine.API
dotnet ef database update
```

Obs: o `database update` depende de um PostgreSQL acessível em `ConnectionStrings:DefaultConnection`.

### Erros comuns no PostgreSQL

| Erro | Causa | O que fazer |
|------|--------|-------------|
| `42501: permission denied for schema public` | O usuário da connection string não pode criar tabelas no `public`. | Ver secção abaixo. |

#### `42501: permission denied for schema public` — o que significa

O PostgreSQL (em especial **15+** ou bases **geridas** Neon/RDS/Supabase) pode negar `CREATE` no schema `default` `public` ao teu utilizador. O EF Core precisa de criar `__EFMigrationsHistory` e as tabelas da app — daí o erro ao arrancar com `Database:ApplyMigrationsOnStartup`.

**A API continua a ouvir na porta 3000**, mas **as migrações não correram**: não há tabelas → login/templates/jobs podem falhar.

**Opções (escolhe uma):**

1. **Migrações com utilizador com permissões** (rápido em dev local)  
   - Na `ConnectionStrings:DefaultConnection`, usa temporariamente um user com direitos de owner/admin (ex.: `postgres` num Postgres local).  
   - `cd backend/KYX.DocEngine.API && dotnet ef database update`  
   - Depois podes voltar ao user “app” se o DBA te der os `GRANT` certos.

2. **Pedir ao DBA** (cloud / produção) — ligado como superuser ou owner da base:

   ```sql
   GRANT CREATE ON SCHEMA public TO seu_usuario_app;
   ```

   Se já existirem tabelas criadas por outro role, podem ser necessários `GRANT` em tabelas/sequences ou `ALTER DEFAULT PRIVILEGES` — o DBA ajusta à política do ambiente.

3. **Base só para ti em Docker/local** — criar DB e user owner desse database; a connection string usa esse user (passa a ter `CREATE` no `public` desse database).

4. **Desligar migração no arranque** (não resolve permissões; só evita o erro no log): em `appsettings` → `"Database": { "ApplyMigrationsOnStartup": false }` e corre `dotnet ef database update` manualmente quando tiveres um user que consiga.

| Erro | Causa | O que fazer |
|------|--------|-------------|
| `42P01: relation "tb_log_requisicao" does not exist` | Migrações não foram aplicadas nesse banco. | `dotnet ef database update` no mesmo host/database da API. |
| API não sobe por causa de `Migrate()` | Startup falhava ao migrar. | A partir desta versão, falha de migração **não encerra** o processo; veja o log `Startup`. Para desligar tentativa automática: `Database:ApplyMigrationsOnStartup` = `false` em `appsettings` e aplique migrações manualmente. |

Enquanto `tb_log_requisicao` não existir, o middleware de auditoria não grava logs (as requisições continuam respondendo normalmente).

### Tabela `tb_log_requisicao` (padrão único)

O DocEngine usa a **mesma tabela** que o NotifyHUB: **`tb_log_requisicao`**. Assim os bancos (dev/homolog/prod ou instâncias diferentes) podem seguir o **mesmo modelo**.

- **DocEngine** grava com `canal = 'docengine'`; `request_payload` / `response_payload` são JSON (endpoint HTTP + corpos).
- **NotifyHUB** (legado) usava outros `canal` (email, sms, etc.) no mesmo esquema de colunas.

A migração `UseTbLogRequisicao` remove a tabela antiga `request_logs` (se existir) e garante `tb_log_requisicao` + índices (`CREATE TABLE IF NOT EXISTS` para ambientes que já tinham a tabela).

