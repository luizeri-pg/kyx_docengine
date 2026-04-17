#!/usr/bin/env python3
"""
Primeira página do PDF → Base64 (só o payload, sem prefixo data:).

Uso:
  python3 pdf-first-page-to-png-b64.py caminho.pdf
  python3 pdf-first-page-to-png-b64.py caminho.pdf --jpeg --max-width 1100

Requer: pip install pymupdf
Saída: stdout = base64 (PNG ou JPEG se --jpeg).
"""
from __future__ import annotations

import argparse
import base64
import sys
from pathlib import Path


def main() -> int:
    p = argparse.ArgumentParser(description="Rasteriza página 1 do PDF para PNG/JPEG em Base64.")
    p.add_argument("pdf", type=Path, help="Ficheiro .pdf")
    p.add_argument("--jpeg", action="store_true", help="JPEG (menor; recomendado para HTML→PDF)")
    p.add_argument("--max-width", type=int, default=1100, help="Largura máxima em px (escala a matriz)")
    args = p.parse_args()
    pdf = args.pdf
    if not pdf.is_file():
        print(f"Ficheiro inexistente: {pdf}", file=sys.stderr)
        return 1
    try:
        import fitz  # PyMuPDF
    except ImportError:
        return 2
    doc = fitz.open(pdf)
    if doc.page_count < 1:
        return 1
    page = doc[0]
    rect = page.rect
    w = max(rect.width, 1.0)
    scale = min(2.0, float(args.max_width) / w)
    mat = fitz.Matrix(scale, scale)
    pix = page.get_pixmap(matrix=mat, alpha=False)
    if args.jpeg:
        raw = pix.tobytes("jpeg", jpg_quality=82)
    else:
        raw = pix.tobytes("png")
    sys.stdout.write(base64.b64encode(raw).decode("ascii"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
