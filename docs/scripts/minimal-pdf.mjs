/**
 * Gera um PDF de 1 página mínimo mas válido (Helvetica), útil para mocks de anexos.
 * @param {string} line — texto numa linha (escapado para sintaxe PDF)
 */
export function createMinimalOnePagePdf(line) {
  const esc = String(line)
    .replace(/\\/g, '\\\\')
    .replace(/\(/g, '\\(')
    .replace(/\)/g, '\\)');
  const stream = `BT /F1 14 Tf 72 720 Td (${esc}) Tj ET`;
  const streamBytes = Buffer.byteLength(stream, 'latin1');

  const objects = [];
  objects[1] = `1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n`;
  objects[2] = `2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n`;
  objects[3] =
    `3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n`;
  objects[4] = `4 0 obj\n<< /Length ${streamBytes} >>\nstream\n${stream}\nendstream\nendobj\n`;
  objects[5] = `5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n`;

  const header = `%PDF-1.4\n%\xE2\xE3\xCF\xD3\n`;
  const pieces = [header];
  const offsets = [0];
  let cursor = Buffer.byteLength(header, 'latin1');

  for (let i = 1; i <= 5; i++) {
    offsets[i] = cursor;
    const body = objects[i];
    pieces.push(body);
    cursor += Buffer.byteLength(body, 'latin1');
  }

  const xrefStart = cursor;
  let xref = `xref\n0 6\n0000000000 65535 f \n`;
  for (let i = 1; i <= 5; i++) {
    xref += `${String(offsets[i]).padStart(10, '0')} 00000 n \n`;
  }
  const trailer = `trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${xrefStart}\n%%EOF\n`;
  pieces.push(xref);
  pieces.push(trailer);

  return Buffer.concat(pieces.map((p) => Buffer.from(p, 'latin1')));
}
