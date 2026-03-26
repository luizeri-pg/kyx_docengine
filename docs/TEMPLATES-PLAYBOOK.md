# Playbook: templates e geração de PDF (DocEngine)

Guia prático para cadastrar templates e alinhar o payload `dados` do `POST /documents/generate`.

## Visão geral

| Tipo | Campo `Template.type` | Conteúdo (`Template.content`) | Motor |
|------|------------------------|----------------------------------|--------|
| HTML | `html` | HTML com placeholders `{{chave}}` | PuppeteerSharp → PDF |
| AcroForm | `acroform` | Arquivo PDF em **base64** (formulário AcroForm) | PdfSharp preenche campos pelo **nome** |

`requiredFields` no template é um **array JSON em string** (ex.: `["nome","cpf"]`), usado para validar que as chaves existem em `dados` antes de enfileirar o job.

---

## 1. Template HTML (`type: html`)

### Placeholders

- Use **`{{nomeDaVariavel}}`** (com espaços opcionais: `{{ nomeDaVariavel }}`).
- A substituição é feita por **regex** no servidor (`PdfEngineService`), **não** é Handlebars completo (sem `{{#if}}`, helpers, etc.).

### Exemplo mínimo de conteúdo

```html
<!DOCTYPE html>
<html>
<body>
  <p>Nome: {{nome}}</p>
  <p>CPF: {{cpf}}</p>
</body>
</html>
```

### `requiredFields`

Ex.: `["nome","cpf"]` — todas as chaves devem estar presentes e não vazias em `dados`.

### PDF

- Página A4, margens fixas no código (`HtmlPdfRenderer`).
- Ajustes visuais finos: fazer no CSS do HTML.

---

## 2. Template AcroForm (`type: acroform`)

### Conteúdo

- O **PDF inteiro** em **base64** no campo `content` do template.
- Os **nomes dos campos** do formulário PDF devem coincidir com as **chaves** em `dados` (ex.: campo `nome_cliente` → `dados.nome_cliente`).

### Tipos de campo suportados (código)

- `PdfTextField` → texto
- `PdfCheckBoxField` → `true` / `1` / `sim` (case-insensitive) para marcado

### Descobrir nomes dos campos (mapear variáveis)

1. Cadastre o template com o PDF em base64 (ou use a tela admin).
2. Chame **`POST /templates/inspect-pdf`** com body `{ "pdfBase64": "<base64>" }`.
3. A resposta lista os **nomes dos campos** detectados — use isso para montar `requiredFields` e o contrato do Core.

### `requiredFields`

Liste exatamente as chaves que o AcroForm deve receber (e que existem como nomes de campo no PDF).

---

## 3. Cadastro de template (API)

- `GET /templates` — lista (sem conteúdo longo em listagem, conforme implementação).
- `GET /templates/{id}` — detalhe com `content`.
- `POST /templates` / `PUT /templates/{id}` — enviar `slug`, `name`, `type` (`html` | `acroform`), `content`, `requiredFields` (array serializado em string JSON no modelo).

Slug é o valor de `config.template` na geração (ex.: `fideliza_termo_genero`).

---

## 4. Checklist antes de ir para produção

- [ ] `requiredFields` bate com as chaves que o Core envia em `dados`.
- [ ] Para **html**: preview mental dos `{{ }}` cobrindo todos os campos obrigatórios.
- [ ] Para **acroform**: `inspect-pdf` executado e nomes de campo conferidos no PDF real.
- [ ] Teste de ponta a ponta: `POST /documents/generate` → polling `GET /documents/status/{jobId}` até `completed` e validação do base64 (PDF abre).

---

## Referências no repositório

- Validação: `DocEngine/Services/TemplateService.cs` — `ValidateRequiredFields`
- HTML: `DocEngine/Services/PdfEngineService.cs` — `HtmlPdfRenderer`, `InjectData`
- AcroForm: `AcroFormPdfRenderer`
- Inspect: `DocEngine/Controllers/TemplatesController.cs` — `inspect-pdf`
