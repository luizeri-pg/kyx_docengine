# DocEngine — Software Design Document (Overview)

## Contexto

O **DocEngine** é um microsserviço responsável por abstrair a geração de documentos PDF.
Ele recebe dados do **KYX Core**, aplica esses dados sobre um template cadastrado e retorna o PDF final em base64.

O projeto já possui:
- Frontend: **Vite (React/TS)**
- Backend: **.NET 10**
- Banco: **PostgreSQL**
- Auth: **Bearer Token (JWT)** — já implementado

---

## Escopo desta implementação

> Apenas a funcionalidade de **geração de PDF** será implementada aqui.
> O módulo de login/auth já está pronto e não deve ser alterado.

---

## Stack

| Camada | Tecnologia |
|---|---|
| API | .NET 10 (C#) — Controllers + Services |
| Fila | Redis + Hangfire (background jobs) |
| Templates | Dois modos: HTML/Handlebars + PDF AcroForm |
| Geração PDF | PuppeteerSharp (HTML→PDF) + PdfSharp (AcroForm) |
| Banco | PostgreSQL via EF Core |
| Storage | Sistema de arquivos local (ou S3 — configurável) |

---

## Módulos a implementar

1. **Template Management** — CRUD de templates (HTML e AcroForm PDF)
2. **Document Generation** — Endpoint `POST /documents/generate`
3. **Queue Worker** — Processamento assíncrono via Hangfire + Redis
4. **Log & Audit** — Registro de cada requisição com `requisicaoId`
5. **Cost Center** — Associação de `centroCusto` em cada geração

---

## Arquivos SDD

| Arquivo | Conteúdo |
|---|---|
| `01-database.md` | Schema do banco de dados |
| `02-api.md` | Contratos de API e implementação dos endpoints |
| `03-pdf-engine.md` | Lógica de geração de PDF (HTML e AcroForm) |
| `04-queue.md` | Fila assíncrona com Hangfire + Redis |
