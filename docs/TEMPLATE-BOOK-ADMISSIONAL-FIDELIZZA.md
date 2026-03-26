# Template `book-admissional-fidelizza-2025`

Template dinâmico em HTML baseado no arquivo **BOOK ADMISSIONAL - FIDELIZZA+ - ATUALIZADO 2025** (7 páginas), para uso em `POST /documents/generate` **sem inlineTemplate**.

## Arquivos gerados

- `docs/templates/template-book-admissional-fidelizza-2025.html`
- `docs/samples/generate-book-admissional-fidelizza-request.json`
- `docs/samples/template-book-admissional-fidelizza-required-fields.json`

## Como usar no padrão da API

1. Cadastrar o template com slug `book-admissional-fidelizza-2025` via `POST /templates`:
   - `type`: `html`
   - `content`: conteúdo de `docs/templates/template-book-admissional-fidelizza-2025.html`
   - `requiredFields`: conteúdo de `docs/samples/template-book-admissional-fidelizza-required-fields.json`

2. Gerar documento:

```bash
curl -s -X POST http://localhost:3000/documents/generate \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d @docs/samples/generate-book-admissional-fidelizza-request.json
```

3. Ler `resultado.jobId` e consultar `GET /documents/status/{jobId}` até `status = completed`.

## Observações de modelagem

- Campos de checkbox foram modelados como strings (`"X"` para marcado, `""` para não marcado).
- O layout foi estruturado em páginas com `page-break-before` para manter formato de book.
- Textos legais podem ser ajustados no HTML sem mudar o contrato da API, desde que preserve os placeholders.
