#!/usr/bin/env python3
"""
Regista o template HTML template-fidelizza via POST /templates (uma vez).
Requer API + JWT. Se o slug já existir, não cria duplicado.
"""
import json
import os
import sys
import urllib.request
import urllib.error
from typing import Optional

BASE = os.environ.get("BASE_URL", "http://127.0.0.1:3000").rstrip("/")
USER = os.environ.get("DOCENGINE_USER", "docengine.demo")
PASSWORD = os.environ.get("DOCENGINE_PASSWORD", "DocEngine@2025")
SLUG = "template-fidelizza"
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
HTML_PATH = os.path.join(ROOT, "docs", "templates", "template-fidelizza.html")

REQUIRED_FIELDS = [
    "nomeCompleto",
    "nomeMae",
    "dataNascimento",
    "naturalidade",
    "estadoCivil",
    "nacionalidade",
    "cpf",
    "rg",
    "tituloEleitor",
    "pis",
    "email",
    "telefone",
    "telefoneFixo",
    "logradouro",
    "complemento",
    "bairroCidadeUf",
    "cep",
    "profissao",
    "empregador",
    "renda",
    "dataEmissao",
    "refInterna",
]


def post_json(url: str, payload: dict, token: Optional[str] = None) -> tuple[int, dict]:
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, method="POST")
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            return r.status, json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="replace")
        try:
            return e.code, json.loads(body)
        except json.JSONDecodeError:
            return e.code, {"raw": body}


def get_json(url: str, token: str) -> Optional[dict]:
    req = urllib.request.Request(url, method="GET")
    req.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError:
        return None


def main() -> int:
    if not os.path.isfile(HTML_PATH):
        print("Ficheiro em falta:", HTML_PATH, file=sys.stderr)
        return 1

    html = open(HTML_PATH, encoding="utf-8").read()
    login_url = f"{BASE}/auth/login"
    templates_url = f"{BASE}/templates"

    _, login = post_json(login_url, {"username": USER, "password": PASSWORD})
    if not login.get("sucesso"):
        print("Login falhou:", login, file=sys.stderr)
        return 1
    token = login["resultado"]["access_token"]

    lst = get_json(templates_url, token)
    if lst:
        items = lst.get("resultado") or []
        for t in items:
            if (t.get("slug") or "").lower() == SLUG:
                print("Template já existe:", SLUG, "id=", t.get("id"))
                return 0
    else:
        print("Aviso: GET /templates falhou; a tentar criar o template mesmo assim...", file=sys.stderr)

    body = {
        "slug": SLUG,
        "name": "Fidelizza — ficha cadastral completa",
        "type": "html",
        "content": html,
        "requiredFields": REQUIRED_FIELDS,
    }
    code, resp = post_json(templates_url, body, token)
    if code == 200 and resp.get("sucesso"):
        print("Template criado:", SLUG, "id=", resp.get("resultado", {}).get("id"))
        return 0
    print("Falha ao criar template (HTTP %s):" % code, json.dumps(resp, ensure_ascii=False), file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
