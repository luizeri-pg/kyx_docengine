# Template `template-fidelizza`

Ficha cadastral completa (mesmo layout da demo “Maria”), com **slug** `template-fidelizza` para uso em **`POST /documents/generate`** **sem** `inlineTemplate`.

## Ficheiros

| Ficheiro | Descrição |
|----------|-----------|
| [`templates/template-fidelizza.html`](templates/template-fidelizza.html) | HTML com placeholders `{{...}}` |
| [`samples/generate-template-fidelizza-request.json`](samples/generate-template-fidelizza-request.json) | Exemplo de **`POST /documents/generate`** só com `requisicaoId`, `config` e `dados` (pessoa fictícia **Beatriz Costa Lima**) |

## 1) Registar o template na API (uma vez)

O HTML não vai no body do `generate`; tem de existir um registo na tabela `templates` com esse slug.

**Opção A — script (recomendado)**

```bash
BASE_URL=http://localhost:3000 python3 scripts/register_template_fidelizza.py
```

**Opção B — `POST /templates`** com corpo JSON contendo `slug`, `name`, `type` (`html`), `content` (texto do HTML) e `requiredFields` (lista de chaves). Pode gerar o JSON com:

```bash
python3 -c "
import json, pathlib
html = pathlib.Path('docs/templates/template-fidelizza.html').read_text(encoding='utf-8')
keys = ['nomeCompleto','nomeMae','dataNascimento','naturalidade','estadoCivil','nacionalidade','cpf','rg','tituloEleitor','pis','email','telefone','telefoneFixo','logradouro','complemento','bairroCidadeUf','cep','profissao','empregador','renda','dataEmissao','refInterna']
print(json.dumps({'slug':'template-fidelizza','name':'Fidelizza — ficha cadastral','type':'html','content':html,'requiredFields':keys}, ensure_ascii=False))
" > /tmp/post-template-fidelizza.json
# Depois: curl -H \"Authorization: Bearer \$TOKEN\" -H \"Content-Type: application/json\" -d @/tmp/post-template-fidelizza.json http://localhost:3000/templates
```

Se o slug já existir, o script não duplica (ver `register_template_fidelizza.py`).

## 2) Gerar o PDF (assíncrono)

```bash
TOKEN=...
curl -s -X POST http://localhost:3000/documents/generate \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d @docs/samples/generate-template-fidelizza-request.json | python3 -m json.tool
```

Resposta: `resultado.jobId` → fazer polling em **`GET /documents/status/{jobId}`** até `status` = `completed`; o PDF está em `resultado.resultado.base64`.

**Script completo** (registo + generate + polling + ficheiro PDF):

```bash
BASE_URL=http://localhost:3000 python3 scripts/generate_pdf_template_fidelizza_async.py
```

Saída típica: `fidelizza-beatriz-costa.pdf` na raiz do repositório.
