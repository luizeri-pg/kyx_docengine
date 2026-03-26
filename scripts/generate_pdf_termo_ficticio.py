#!/usr/bin/env python3
"""
Gera PDF via POST /documents/generate-sync — padrão tipo termo (nome, cpf, nameMae, dataNascimento).
Dados fictícios. Grava o request JSON completo em docs/samples/termo-ficticio-full-request.json
"""
import base64
import json
import os
import sys
import urllib.request
from typing import Optional

BASE = os.environ.get("BASE_URL", "http://127.0.0.1:3000").rstrip("/")
USER = os.environ.get("DOCENGINE_USER", "docengine.demo")
PASSWORD = os.environ.get("DOCENGINE_PASSWORD", "DocEngine@2025")
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_PDF = os.environ.get("OUT_PDF", os.path.join(ROOT, "termo-ficticio.pdf"))
OUT_REQ = os.environ.get(
    "OUT_REQUEST_JSON",
    os.path.join(ROOT, "docs", "samples", "termo-ficticio-full-request.json"),
)

HTML = """<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="utf-8"/>
<style>
  body { font-family: 'Segoe UI', system-ui, sans-serif; margin: 0; padding: 48px; background: #f4f6f8; color: #222; }
  .box { background: #fff; max-width: 640px; margin: 0 auto; padding: 40px; border-radius: 8px; box-shadow: 0 2px 12px rgba(0,0,0,.08); }
  h1 { font-size: 20px; color: #0b5c7a; border-bottom: 2px solid #0b5c7a; padding-bottom: 12px; }
  p { line-height: 1.55; font-size: 14px; margin: 14px 0; }
  .k { color: #555; font-weight: 600; }
  .foot { margin-top: 32px; font-size: 11px; color: #888; text-align: center; }
</style>
</head>
<body>
<div class="box">
  <h1>Termo — demonstração (dados fictícios)</h1>
  <p><span class="k">Nome completo:</span> {{nome}}</p>
  <p><span class="k">CPF:</span> {{cpf}}</p>
  <p><span class="k">Nome da mãe:</span> {{nameMae}}</p>
  <p><span class="k">Data de nascimento:</span> {{dataNascimento}}</p>
  <p>Declaro para os devidos fins que as informações acima correspondem a dados de teste, sem valor legal.</p>
  <p class="foot">Documento gerado pelo DocEngine — conteúdo inteiramente fictício.</p>
</div>
</body>
</html>"""

DADOS = {
    "nome": "ANA CAROLINA MENDES",
    "cpf": "387.654.321-00",
    "nameMae": "JULIANA MENDES SOUZA",
    "dataNascimento": "05/03/1995",
}

REQUIRED = ["nome", "cpf", "nameMae", "dataNascimento"]


def post_json(url: str, payload: dict, token: Optional[str] = None) -> dict:
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, method="POST")
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    with urllib.request.urlopen(req, timeout=300) as r:
        return json.loads(r.read().decode("utf-8"))


def main() -> int:
    login_url = f"{BASE}/auth/login"
    gen_url = f"{BASE}/documents/generate-sync"

    body = {
        "requisicaoId": "termo-ficticio-001",
        "nomeArquivo": "termo-ficticio.pdf",
        "inlineTemplate": {
            "type": "html",
            "content": HTML,
            "requiredFields": REQUIRED,
        },
        "dados": DADOS,
    }

    os.makedirs(os.path.dirname(OUT_REQ), exist_ok=True)
    with open(OUT_REQ, "w", encoding="utf-8") as f:
        json.dump(body, f, ensure_ascii=False, indent=2)
    print("Request JSON completo gravado em:", OUT_REQ)

    try:
        login = post_json(login_url, {"username": USER, "password": PASSWORD})
    except Exception as e:
        print("Erro no login:", e, file=sys.stderr)
        return 1

    if not login.get("sucesso"):
        print("Login falhou:", login, file=sys.stderr)
        return 1

    token = login["resultado"]["access_token"]

    try:
        resp = post_json(gen_url, body, token)
    except Exception as e:
        print("Erro em generate-sync:", e, file=sys.stderr)
        return 1

    if not resp.get("sucesso"):
        print("generate-sync falhou:", json.dumps(resp, indent=2, ensure_ascii=False), file=sys.stderr)
        return 1

    b64 = resp["resultado"]["base64"]
    raw = base64.b64decode(b64)
    with open(OUT_PDF, "wb") as f:
        f.write(raw)

    print("PDF gravado:", os.path.abspath(OUT_PDF))
    print("Tamanho PDF:", len(raw), "bytes")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
