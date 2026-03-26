# Subir a API e gerar PDF (ex.: Maria) — **sem alterar a base de dados**

> **Política atual:** não executar migrações, scripts SQL nem `INSERT` na BD. Usar apenas a **connection string e utilizadores que já existem** no vosso ambiente (ou o que a equipa de infra vos indicar).

---

## 1) Backend

```bash
cd backend/KYX.DocEngine.API
dotnet restore
dotnet run
```

Confirme: `GET http://localhost:3000/health` → **200**.

---

## 2) Credenciais para `POST /auth/login`

Use **username** e **password** de um utilizador **já existente** em `tb_usuario` (conforme o mapeamento `Schema:Usuario` da vossa configuração).

Os exemplos nos scripts (`docengine.demo` / `DocEngine@2025`) são só **placeholder** — substituam pelos valores reais ou definam variáveis de ambiente (ver script abaixo).

---

## 3) Gerar o PDF da Maria via API (igual ao body em `docs/samples/maria-generate-sync-body.json`)

**Script (recomendado)**

```bash
chmod +x scripts/gerar-pdf-maria-api.sh
BASE_URL=http://localhost:3000 \
DOCENGINE_USER='o_vosso_username' \
DOCENGINE_PASS='a_vossa_senha' \
./scripts/gerar-pdf-maria-api.sh
```

**Python**

```bash
BASE_URL=http://localhost:3000 \
DOCENGINE_USER='o_vosso_username' \
DOCENGINE_PASSWORD='a_vossa_senha' \
python3 scripts/generate_pdf_maria_ficticio.py
```

**Corpo JSON:** `docs/samples/maria-generate-sync-body.json` — enviar em `POST /documents/generate-sync` com header `Authorization: Bearer <token>`.

Requisitos do endpoint:

- Ambiente **Development** (ou `Documents:AllowSyncPdfGeneration: true`).
- JWT válido (passo login).

---

## 4) Frontend (opcional)

```bash
cd frontend && npm install && npm run dev
```

O browser usa a API em `http://localhost:3000` (ajustem `VITE_API_URL` no `.env` se for outro host).

---

## Resumo dos endpoints

| Passo | Método | URL |
|--------|--------|-----|
| Login | `POST` | `/auth/login` |
| PDF síncrono (Maria) | `POST` | `/documents/generate-sync` |
| Health | `GET` | `/health` |

---

## Anexo — só quando **for permitido** alterar a BD

*(Migrações, `appsettings.Local.json` para BD nova, Docker Postgres, scripts `docs/sql/insert-usuario-*.sql`.)*

Neste momento **não aplicar** sem alinhamento com DBA/infra. Ficheiros de referência para uma fase futura:

- `backend/KYX.DocEngine.API/appsettings.Local.json.example.fresh-db` — exemplo de schema “moderno” + migrações no arranque (uso **não** obrigatório).
- `docs/sql/insert-usuario-pos-migracoes.sql` — utilizador demo **apenas** se a tabela seguir o modelo pós-migrações EF.
- `docs/sql/insert-usuario-legado-tb_usuario.sql` — ambiente com colunas legadas (`str_login`, etc.), alinhado com `appsettings.Development.json`.

Se a vossa BD já está correta, **não** é necessário nada disto — basta apontar a connection string (ex. `appsettings.Local.json` só com `ConnectionStrings`, sem tocar em tabelas).
