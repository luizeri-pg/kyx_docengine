# Fidelizza 2025 - documentos separados

Arquivos gerados a partir de `BOOK ADMISSIONAL - FIDELIZZA+ - ATUALIZADO 2025. (4).pdf`:

- `01-checklist-admissional.pdf`
- `02-autodeclaracao-racial.pdf`
- `03-adiantamento-salarial.pdf`
- `04-formulario-4.pdf`
- `05-regras-vale-transporte.pdf`
- `06-declaracao-identidade-genero.pdf`
- `07-declaracao-orientacao-sexual.pdf`

## Slugs sugeridos para templates

- `fidelizza_2025_checklist_admissional`
- `fidelizza_2025_autodeclaracao_racial`
- `fidelizza_2025_adiantamento_salarial`
- `fidelizza_2025_formulario_4`
- `fidelizza_2025_regras_vale_transporte`
- `fidelizza_2025_declaracao_identidade_genero`
- `fidelizza_2025_declaracao_orientacao_sexual`

## Logo em Base64 (template HTML)

Se o template for do tipo `html`, inclua no conteúdo:

```html
<img
  alt="Logo"
  style="max-height: 72px; object-fit: contain;"
  src="data:image/png;base64,{{logoBase64}}"
/>
```

E no `dados` envie:

```json
{
  "logoBase64": "iVBORw0KGgoAAAANSUhEUgAA..."
}
```

## Observação

Para template `acroform`, o `content` deve ser o PDF em Base64 (o logo precisa estar no próprio PDF, ou em campo específico do formulário caso exista).
