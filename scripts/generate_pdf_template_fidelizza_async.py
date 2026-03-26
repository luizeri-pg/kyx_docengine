#!/usr/bin/env python3
"""
1) Garante template template-fidelizza (POST /templates se necessário)
2) POST /documents/generate com docs/samples/generate-template-fidelizza-request.json
3) Polling GET /documents/status/{jobId} até completed → grava PDF
"""
import base64
import json
import os
import sys
import time
import urllib.request
import urllib.error
from typing import Any, Dict, Optional

BASE = os.environ.get("BASE_URL", "http://127.0.0.1:3000").rstrip("/")
USER = os.environ.get("DOCENGINE_USER", "docengine.demo")
PASSWORD = os.environ.get("DOCENGINE_PASSWORD", "DocEngine@2025")
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
GENERATE_JSON = os.path.join(ROOT, "docs", "samples", "generate-template-fidelizza-request.json")
OUT_PDF = os.environ.get("OUT_PDF", os.path.join(ROOT, "fidelizza-beatriz-costa.pdf"))


def req(
    url: str,
    method: str = "GET",
    payload: Optional[dict] = None,
    token: Optional[str] = None,
) -> tuple[int, dict]:
    data = None
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
    r = urllib.request.Request(url, data=data, method=method)
    if payload is not None:
        r.add_header("Content-Type", "application/json")
    if token:
        r.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(r, timeout=300) as resp:
            return resp.status, json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="replace")
        try:
            return e.code, json.loads(body)
        except json.JSONDecodeError:
            return e.code, {"raw": body}


def main() -> int:
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import register_template_fidelizza as reg

    if reg.main() != 0:
        print("Aviso: registo do template falhou; a geração pode retornar 404.", file=sys.stderr)

    _, login = req(f"{BASE}/auth/login", "POST", {"username": USER, "password": PASSWORD})
    if not login.get("sucesso"):
        print("Login falhou:", login, file=sys.stderr)
        return 1
    token = login["resultado"]["access_token"]

    with open(GENERATE_JSON, encoding="utf-8") as f:
        gen_body = json.load(f)

    print("POST /documents/generate")
    print(json.dumps(gen_body, ensure_ascii=False, indent=2))

    code, resp = req(f"{BASE}/documents/generate", "POST", gen_body, token)
    if code != 200 or not resp.get("sucesso"):
        print("generate falhou:", code, json.dumps(resp, ensure_ascii=False), file=sys.stderr)
        return 1

    job_id = resp["resultado"]["jobId"]
    print("jobId:", job_id)

    for i in range(90):
        time.sleep(2)
        _, st = req(f"{BASE}/documents/status/{job_id}", "GET", None, token)
        if not st.get("sucesso"):
            print("status erro:", st, file=sys.stderr)
            return 1
        inner = st["resultado"]
        status = inner.get("status")
        print(f"[{i + 1}] status={status}")
        if status == "failed":
            print(json.dumps(st, indent=2, ensure_ascii=False), file=sys.stderr)
            return 1
        if status == "completed":
            b64 = (inner.get("resultado") or {}).get("base64")
            if not b64:
                print("sem base64:", st, file=sys.stderr)
                return 1
            raw = base64.b64decode(b64)
            with open(OUT_PDF, "wb") as fp:
                fp.write(raw)
            print("PDF:", os.path.abspath(OUT_PDF), "bytes:", len(raw))
            return 0

    print("Timeout.", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
