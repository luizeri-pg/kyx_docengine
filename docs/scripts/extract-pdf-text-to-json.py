#!/usr/bin/env python3
"""
Extrai texto de um PDF para JSON (uma entrada por página).

Uso:
  python3 docs/scripts/extract-pdf-text-to-json.py docs/preview/dossie-simplix-mock.pdf
  python3 docs/scripts/extract-pdf-text-to-json.py docs/preview/dossie-simplix-mock.pdf docs/preview/dossie-simplix-mock.extracted.json

Requer: pip install pymupdf
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser(description="Extrai texto do PDF para JSON.")
    parser.add_argument("pdf", type=Path, help="Caminho do .pdf")
    parser.add_argument(
        "saida",
        type=Path,
        nargs="?",
        help="Arquivo .json de saída (padrão: <pdf>.extracted.json)",
    )
    args = parser.parse_args()

    try:
        import fitz  # PyMuPDF
    except ImportError:
        print(
            "Instale PyMuPDF: pip install pymupdf",
            file=sys.stderr,
        )
        return 1

    pdf_path: Path = args.pdf
    if not pdf_path.is_file():
        print(f"Arquivo não encontrado: {pdf_path}", file=sys.stderr)
        return 1

    out_path = args.saida
    if out_path is None:
        out_path = pdf_path.with_suffix(".extracted.json")

    doc = fitz.open(pdf_path)
    try:
        paginas = []
        for i in range(doc.page_count):
            page = doc[i]
            texto = page.get_text("text") or ""
            paginas.append(
                {
                    "numero": i + 1,
                    "texto": texto.strip(),
                    "caracteres": len(texto),
                }
            )
        payload = {
            "fonte": str(pdf_path.as_posix()),
            "totalPaginas": doc.page_count,
            "paginas": paginas,
        }
    finally:
        doc.close()

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(out_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
