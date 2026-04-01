/** Converte valor JSON em registro plano chave → string (valores para POST /documents/generate). */

function isPlainObject(v: unknown): v is Record<string, unknown> {
  return v !== null && typeof v === 'object' && !Array.isArray(v);
}

export function flattenDadosToStringRecord(value: unknown, prefix = ''): Record<string, string> {
  const out: Record<string, string> = {};

  const setLeaf = (key: string, v: unknown) => {
    if (key === '') return;
    out[key] = v === null || v === undefined ? '' : String(v);
  };

  if (value === null || value === undefined) {
    if (prefix) setLeaf(prefix, '');
    return out;
  }

  if (typeof value !== 'object') {
    setLeaf(prefix, value);
    return out;
  }

  if (Array.isArray(value)) {
    value.forEach((item, index) => {
      const path = prefix ? `${prefix}.${index}` : String(index);
      if (isPlainObject(item)) {
        Object.assign(out, flattenDadosToStringRecord(item, path));
      } else if (Array.isArray(item)) {
        Object.assign(out, flattenDadosToStringRecord(item, path));
      } else {
        setLeaf(path, item);
      }
    });
    return out;
  }

  for (const [k, v] of Object.entries(value)) {
    const path = prefix ? `${prefix}.${k}` : k;
    if (isPlainObject(v)) {
      Object.assign(out, flattenDadosToStringRecord(v, path));
    } else if (Array.isArray(v)) {
      Object.assign(out, flattenDadosToStringRecord(v, path));
    } else {
      setLeaf(path, v);
    }
  }
  return out;
}

/** Detecta se algum valor de primeiro nível é objeto ou array (precisa achatar). */
export function dadosRootNeedsFlatten(root: Record<string, unknown>): boolean {
  return Object.values(root).some((v) => v !== null && typeof v === 'object');
}

/**
 * Parse do textarea JSON → `Record<string, string>` aceito pela API.
 * Objetos/listas viram chaves com pontos e índices: `empresa.nome`, `exames.0.tuss`.
 */
export function parseDadosJsonForApi(text: string): Record<string, string> {
  const parsed = JSON.parse(text) as unknown;
  if (!isPlainObject(parsed)) {
    throw new Error('A raiz do JSON deve ser um objeto { ... }.');
  }
  if (dadosRootNeedsFlatten(parsed)) {
    return flattenDadosToStringRecord(parsed);
  }
  return Object.fromEntries(
    Object.entries(parsed).map(([k, v]) => [k, v === null || v === undefined ? '' : String(v)])
  );
}
