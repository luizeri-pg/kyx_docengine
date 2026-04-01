/**
 * Decodifica o payload (2.º segmento) de um JWT.
 * JWT usa Base64URL; `atob` só aceita Base64 clássico (+, /, padding).
 */
export function decodeJwtPayload(token: string): Record<string, unknown> {
  const part = token.split('.')[1];
  if (!part) {
    throw new Error('Token JWT sem payload');
  }
  const base64 = part.replace(/-/g, '+').replace(/_/g, '/');
  const pad = base64.length % 4;
  const padded = pad ? base64 + '='.repeat(4 - pad) : base64;
  const json = atob(padded);
  return JSON.parse(json) as Record<string, unknown>;
}
