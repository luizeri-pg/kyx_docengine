# Inventário de tabelas — KYX / DocEngine

Este documento consolida **todas as tabelas** referenciadas no repositório: **módulo PDF (DocEngine)** e **plataforma compartilhada** (padrão Notify/KYX, prefixo `tb_`).

---

## Visão rápida

| Tabela PostgreSQL | Módulo | Uso no DocEngine (.NET) |
|-------------------|--------|---------------------------|
| **`templates`** | DocEngine PDF | Templates de documento (slug, HTML/AcroForm). Ver [SDD/01-database.md](../SDD/01-database.md). |
| **`document_jobs`** | DocEngine PDF | Fila de geração de PDF (status, base64, etc.). |
| **`tb_log_requisicao`** | Plataforma | Auditoria HTTP unificada (Notify, DocEngine `canal = docengine`, etc.). |
| **`tb_usuario`** | Plataforma | Login (`email`/`nome` + `senha` BCrypt, `perfil_id`). |
| **`tb_perfil`** | Plataforma | Perfis de acesso. |
| **`tb_role`** | Plataforma | Papéis (roles). |
| **`tb_perfil_role`** | Plataforma | N:N perfil ↔ role. |
| **`tb_integracao`** | Plataforma | Cadastro de integrações (credenciais JSON, canal). |
| **`tb_log_integracao`** | Plataforma | Log por chamada a integração (FK lógica a `requisicao_id` em `tb_log_requisicao`). |
| **`tb_consumo`** | Plataforma | Consumo por requisição/integração/centro de custo. |
| **`tb_template`** | Notify (mensagens) | Templates de **notificação** (email/SMS/WhatsApp) — **não** é o mesmo que `templates` do PDF. |

Tabelas **legadas / removidas** no fluxo atual:

| Tabela | Nota |
|--------|------|
| `request_logs` | Substituída por `tb_log_requisicao`. |

---

## Relacionamentos (plataforma)

```
tb_perfil ──< tb_usuario
tb_perfil ──< tb_perfil_role >── tb_role

tb_log_requisicao ──< tb_log_integracao >── tb_integracao
tb_log_requisicao ──< tb_consumo >── tb_integracao
```

Chaves estrangeiras de `tb_log_integracao` e `tb_consumo` para `tb_log_requisicao` usam **`requisicao_id`** (valor de negócio), não o `id` da linha — ver `DocEngineDbContext`.

---

## DocEngine PDF (independente do Notify)

- **`templates`** ↔ **`document_jobs`**: FK `template_id` → `templates.id`.
- Estas tabelas seguem o [SDD/01-database.md](../SDD/01-database.md); nomes **sem** prefixo `tb_` por histórico de migrações.

---

## Ficheiros de referência SQL

- [`docs/sql/tb_usuario.sql`](sql/tb_usuario.sql) — criação idempotente de `tb_usuario`.
- [`docs/sql/plataforma-kyx-tabelas.sql`](sql/plataforma-kyx-tabelas.sql) — **todas** as tabelas `tb_*` da plataforma (perfil, role, integração, consumo, log de integração, template de notificação) + índice único em `tb_log_requisicao.requisicao_id` + FK `tb_usuario` → `tb_perfil`.
- Migração EF **`MapPlataformaKyxTabelas`** aplica o mesmo SQL de forma idempotente.

---

## Entidades no código (`DocEngineDbContext`)

Todas mapeadas em `KYX.DocEngine.API.Data.DocEngineDbContext` (namespace `KYX.DocEngine.API.Models.Entities`), para consultas e evolução futura (dashboard, admin, relatórios).

**Importante:** `Template` (tabela `templates`) é o **template de PDF**; **`NotificacaoTemplate`** (tabela `tb_template`) é o template de **notificação** do ecossistema Notify — **não misturar**.

---

## Próximos passos sugeridos

1. **Usuários admin**: passar `UsuariosController` de memória para `tb_usuario` + `tb_perfil` quando quiserem persistência total.
2. **Renomear** `templates` / `document_jobs` para `tb_docengine_*` (opcional): exige migração de dados e alinhamento com DBA.
3. **Consumo / integrações**: gravar `tb_consumo` e `tb_log_integracao` quando o DocEngine chamar serviços externos rastreáveis.
