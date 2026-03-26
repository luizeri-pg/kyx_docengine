#!/usr/bin/env python3
"""
Gera PDF via POST /documents/generate-sync com dados fictícios de "Maria".
Requer API em execução (ex.: dotnet run) e Documents:AllowSyncPdfGeneration=true.
Uso: BASE_URL=http://127.0.0.1:3000 python3 scripts/generate_pdf_maria_ficticio.py
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
OUT = os.environ.get(
    "OUT_PDF",
    os.path.join(os.path.dirname(__file__), "..", "maria-dados-ficticios.pdf"),
)

HTML = """<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="utf-8"/>
<style>
  * { box-sizing: border-box; }
  body { font-family: 'Segoe UI', system-ui, sans-serif; margin: 0; padding: 40px; background: #eef2f5; color: #1a1a1a; }
  .sheet { background: #fff; max-width: 720px; margin: 0 auto; padding: 36px 40px; border-radius: 8px; box-shadow: 0 2px 12px rgba(0,0,0,.08); }
  h1 { font-size: 22px; color: #0b5c7a; margin: 0 0 8px; border-bottom: 2px solid #0b5c7a; padding-bottom: 12px; }
  .sub { color: #666; font-size: 13px; margin-bottom: 24px; }
  h2 { font-size: 14px; text-transform: uppercase; letter-spacing: .06em; color: #0b5c7a; margin: 22px 0 10px; }
  table { width: 100%; border-collapse: collapse; font-size: 13px; }
  td { padding: 8px 10px; border-bottom: 1px solid #e8ecef; vertical-align: top; }
  td.k { width: 38%; color: #555; font-weight: 600; }
  td.v { color: #111; }
  .badge { display: inline-block; background: #e3f2f7; color: #0b5c7a; padding: 4px 10px; border-radius: 4px; font-size: 12px; margin-bottom: 16px; }
  .footer { margin-top: 28px; font-size: 11px; color: #888; text-align: center; }
</style>
</head>
<body>
<div class="sheet">
  <span class="badge">Documento de demonstração — dados fictícios</span>
  <h1>Ficha cadastral — {{nomeCompleto}}</h1>
  <p class="sub">Emitido em {{dataEmissao}} · Referência interna {{refInterna}}</p>

  <h2>Dados pessoais</h2>
  <table>
    <tr><td class="k">Nome completo</td><td class="v">{{nomeCompleto}}</td></tr>
    <tr><td class="k">Nome da mãe</td><td class="v">{{nomeMae}}</td></tr>
    <tr><td class="k">Data de nascimento</td><td class="v">{{dataNascimento}}</td></tr>
    <tr><td class="k">Naturalidade</td><td class="v">{{naturalidade}}</td></tr>
    <tr><td class="k">Estado civil</td><td class="v">{{estadoCivil}}</td></tr>
    <tr><td class="k">Nacionalidade</td><td class="v">{{nacionalidade}}</td></tr>
  </table>

  <h2>Documentos</h2>
  <table>
    <tr><td class="k">CPF</td><td class="v">{{cpf}}</td></tr>
    <tr><td class="k">RG</td><td class="v">{{rg}}</td></tr>
    <tr><td class="k">Título de eleitor</td><td class="v">{{tituloEleitor}}</td></tr>
    <tr><td class="k">PIS/PASEP</td><td class="v">{{pis}}</td></tr>
  </table>

  <h2>Contato</h2>
  <table>
    <tr><td class="k">E-mail</td><td class="v">{{email}}</td></tr>
    <tr><td class="k">Telefone celular</td><td class="v">{{telefone}}</td></tr>
    <tr><td class="k">Telefone fixo</td><td class="v">{{telefoneFixo}}</td></tr>
  </table>

  <h2>Endereço</h2>
  <table>
    <tr><td class="k">Logradouro</td><td class="v">{{logradouro}}</td></tr>
    <tr><td class="k">Complemento</td><td class="v">{{complemento}}</td></tr>
    <tr><td class="k">Bairro / Cidade / UF</td><td class="v">{{bairroCidadeUf}}</td></tr>
    <tr><td class="k">CEP</td><td class="v">{{cep}}</td></tr>
  </table>

  <h2>Profissional</h2>
  <table>
    <tr><td class="k">Profissão</td><td class="v">{{profissao}}</td></tr>
    <tr><td class="k">Empregador (fictício)</td><td class="v">{{empregador}}</td></tr>
    <tr><td class="k">Renda mensal declarada</td><td class="v">{{renda}}</td></tr>
  </table>

  <p class="footer">Todos os dados acima são <strong>fictícios</strong>, apenas para testes do motor de documentos.</p>
</div>
</body>
</html>"""

DADOS = {
    "nomeCompleto": "Maria Silva Santos",
    "nomeMae": "Ana Paula Oliveira Santos",
    "dataNascimento": "15/08/1987",
    "naturalidade": "São Paulo — SP",
    "estadoCivil": "Solteira",
    "nacionalidade": "Brasileira",
    "cpf": "529.982.247-25",
    "rg": "45.678.912-3 SSP/SP",
    "tituloEleitor": "1234 5678 9012",
    "pis": "123.45678.90-1",
    "email": "maria.silva.exemplo@email.com.br",
    "telefone": "(11) 98765-4321",
    "telefoneFixo": "(11) 3456-7890",
    "logradouro": "Rua das Flores, 123",
    "complemento": "Apto 45 — Bloco B",
    "bairroCidadeUf": "Jardim Primavera — São Paulo / SP",
    "cep": "01234-567",
    "profissao": "Analista de sistemas",
    "empregador": "TechSoluções Ltda. (nome fictício)",
    "renda": "R$ 8.450,00",
    "dataEmissao": "23/03/2026",
    "refInterna": "DEMO-MARIA-2026-001",
}

REQUIRED = list(DADOS.keys())


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
        "requisicaoId": "maria-ficticio-001",
        "nomeArquivo": "maria-dados-ficticios.pdf",
        "inlineTemplate": {
            "type": "html",
            "content": HTML,
            "requiredFields": REQUIRED,
        },
        "dados": DADOS,
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
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "wb") as f:
        f.write(raw)

    print("PDF criado:", out)
    print("Tamanho:", len(raw), "bytes")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
