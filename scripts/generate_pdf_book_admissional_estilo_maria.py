#!/usr/bin/env python3
"""
Gera PDF do book admissional Fidelizza+ com o template estilo Maria (HTML em docs/templates).

Requer API em execução (ex.: dotnet run) e Documents:AllowSyncPdfGeneration=true.

Uso:
  BASE_URL=http://127.0.0.1:3000 python3 scripts/generate_pdf_book_admissional_estilo_maria.py

Opcional:
  TEMPLATE_HTML=docs/templates/template-book-admissional-fidelizza-2025.html
  OUT_PDF=book-admissional-estilo-maria.pdf
"""
import base64
import json
import os
import sys
import urllib.request
from typing import Any, Optional

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
BASE = os.environ.get("BASE_URL", "http://127.0.0.1:3000").rstrip("/")
USER = os.environ.get("DOCENGINE_USER", "docengine.demo")
PASSWORD = os.environ.get("DOCENGINE_PASSWORD", "DocEngine@2025")
OUT = os.environ.get(
    "OUT_PDF",
    os.path.join(ROOT, "book-admissional-estilo-maria.pdf"),
)
TEMPLATE_PATH = os.path.abspath(
    os.environ.get(
        "TEMPLATE_HTML",
        os.path.join(ROOT, "docs", "templates", "template-book-admissional-fidelizza-2025.html"),
    )
)
SAMPLE_REQUEST = os.path.join(ROOT, "docs", "samples", "generate-book-admissional-fidelizza-request.json")
SAMPLE_REQUIRED = os.path.join(
    ROOT, "docs", "samples", "template-book-admissional-fidelizza-required-fields.json"
)


def load_json(path: str) -> Any:
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def post_json(url: str, payload: dict, token: Optional[str] = None) -> dict:
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, method="POST")
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    with urllib.request.urlopen(req, timeout=300) as r:
        return json.loads(r.read().decode("utf-8"))


def main() -> int:
    if not os.path.isfile(TEMPLATE_PATH):
        print("Template HTML não encontrado:", TEMPLATE_PATH, file=sys.stderr)
        return 1
    if not os.path.isfile(SAMPLE_REQUEST):
        print("JSON de exemplo não encontrado:", SAMPLE_REQUEST, file=sys.stderr)
        return 1
    if not os.path.isfile(SAMPLE_REQUIRED):
        print("requiredFields JSON não encontrado:", SAMPLE_REQUIRED, file=sys.stderr)
        return 1

    with open(TEMPLATE_PATH, encoding="utf-8") as f:
        html = f.read()

    sample = load_json(SAMPLE_REQUEST)
    dados = sample["dados"]
    required = load_json(SAMPLE_REQUIRED)
    requisicao_id = sample.get("requisicaoId", "book-admissional-estilo-maria-001")
    nome_cfg = (sample.get("config") or {}).get("nomeArquivo")
    nome_arquivo = os.path.basename(OUT) if OUT.endswith(".pdf") else (nome_cfg or "book-admissional-estilo-maria.pdf")

    login_url = f"{BASE}/auth/login"
    gen_url = f"{BASE}/documents/generate-sync"

    try:
        login = post_json(
            login_url,
            {"username": USER, "password": PASSWORD},
        )
    except Exception as e:
        print("Erro no login:", e, file=sys.stderr)
        return 1

    if not login.get("sucesso"):
        print("Login falhou:", login, file=sys.stderr)
        return 1

    token = login["resultado"]["access_token"]

    body = {
        "requisicaoId": requisicao_id,
        "nomeArquivo": nome_arquivo,
        "inlineTemplate": {
            "type": "html",
            "content": html,
            "requiredFields": required,
        },
        "dados": dados,
    }

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
    out = os.path.abspath(OUT)
    parent = os.path.dirname(out)
    if parent:
        os.makedirs(parent, exist_ok=True)
    with open(out, "wb") as f:
        f.write(raw)

    print("PDF criado:", out)
    print("Tamanho:", len(raw), "bytes")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
