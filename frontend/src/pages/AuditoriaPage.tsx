import { FormEvent, useState } from 'react';
import { Search } from 'lucide-react';
import { auditApi, type RequestLogEntry } from '../services/api';

function formatJsonOrText(s: string) {
  try {
    const o = JSON.parse(s) as unknown;
    return JSON.stringify(o, null, 2);
  } catch {
    return s;
  }
}

/** Layout referência: Auditoria & Logs — busca por Request ID (UUID). */
export function AuditoriaPage() {
  const [requisicaoId, setRequisicaoId] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [logs, setLogs] = useState<RequestLogEntry[] | null>(null);
  const [searched, setSearched] = useState(false);

  const consultar = async (event: FormEvent) => {
    event.preventDefault();
    const id = requisicaoId.trim();
    if (!id) return;
    setLoading(true);
    setError('');
    setSearched(true);
    try {
      const res = await auditApi.logsByRequisicao(id);
      setLogs(res.resultado ?? []);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { mensagem?: string } } };
      setError(e?.response?.data?.mensagem || 'Não foi possível carregar os logs.');
      setLogs([]);
    } finally {
      setLoading(false);
    }
  };

  const showBigEmpty = !loading && !searched;
  const showNoResults = searched && !loading && !error && logs && logs.length === 0;
  const showResults = searched && !loading && logs && logs.length > 0;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-white">Auditoria &amp; Logs</h2>
        <p className="text-kyx-400 mt-1 max-w-2xl">Consulte logs completos por Request ID</p>
      </div>

      <div className="rounded-xl border border-kyx-800/40 bg-kyx-950/40 p-5 md:p-6">
        <form onSubmit={consultar} className="space-y-3">
          <label className="block text-sm font-medium text-kyx-300">Request ID (UUID)</label>
          <div className="flex flex-col sm:flex-row gap-3">
            <input
              className="input flex-1 font-mono text-sm bg-warning-950/20 border-warning-700/30 placeholder:text-kyx-500 focus:ring-warning-600/50"
              value={requisicaoId}
              onChange={(e) => setRequisicaoId(e.target.value)}
              placeholder="Ex: 123e4567-e89b-12d3-a456-426614174000"
              autoComplete="off"
            />
            <button
              type="submit"
              disabled={loading}
              className="inline-flex items-center justify-center gap-2 px-6 py-3 rounded-lg font-medium text-white shrink-0 bg-gradient-to-r from-warning-600 to-warning-700 hover:from-warning-500 hover:to-warning-600 focus:outline-none focus:ring-2 focus:ring-warning-500 focus:ring-offset-2 focus:ring-offset-kyx-950 disabled:opacity-60"
            >
              <Search className="w-4 h-4" />
              {loading ? 'Buscando…' : 'Buscar'}
            </button>
          </div>
        </form>
      </div>

      {error && (
        <div className="p-3 rounded-lg bg-danger-500/10 border border-danger-500/30 text-danger-500 text-sm">{error}</div>
      )}

      <div className="rounded-xl border border-kyx-800/40 bg-kyx-950/30 min-h-[320px] flex flex-col">
        {loading && (
          <div className="flex-1 flex items-center justify-center p-12 text-kyx-400">Carregando…</div>
        )}

        {!loading && showBigEmpty && (
          <div className="flex-1 flex flex-col items-center justify-center text-center px-6 py-16">
            <Search className="w-16 h-16 text-warning-600/80 mb-6 stroke-[1]" />
            <h3 className="text-lg font-semibold text-white mb-2">Busque por Request ID</h3>
            <p className="text-kyx-400 max-w-md text-sm">
              Digite o UUID da requisição para visualizar os logs completos
            </p>
          </div>
        )}

        {!loading && showNoResults && (
          <div className="flex-1 flex flex-col items-center justify-center text-center px-6 py-16">
            <Search className="w-14 h-14 text-warning-600/60 mb-4 stroke-[1]" />
            <p className="text-warning-500/90 font-medium">Nenhum log encontrado para este ID.</p>
            <p className="text-kyx-500 text-sm mt-2">Verifique o UUID ou se a requisição já foi registrada.</p>
          </div>
        )}

        {!loading && showResults && (
          <div className="p-5 md:p-6 space-y-4 max-h-[70vh] overflow-y-auto">
            <p className="text-sm text-kyx-400">
              {logs!.length} registro(s) encontrado(s) para este Request ID.
            </p>
            {logs!.map((log) => (
              <div
                key={log.id}
                className="rounded-lg border border-kyx-800/50 bg-kyx-900/40 p-4 space-y-3"
              >
                <div className="flex flex-wrap gap-2 text-xs text-kyx-400 items-center">
                  {log.canal && (
                    <span className="px-2 py-0.5 rounded bg-warning-500/15 text-warning-600/90 capitalize">{log.canal}</span>
                  )}
                  <span className="font-mono text-warning-500/90">{log.endpoint}</span>
                  {log.httpStatusCode != null && (
                    <span className="px-2 py-0.5 rounded bg-kyx-800/80 text-kyx-200">HTTP {log.httpStatusCode}</span>
                  )}
                  {log.durationMs != null && <span>{log.durationMs}ms</span>}
                  <span>{new Date(log.createdAt).toLocaleString('pt-BR')}</span>
                </div>
                {log.centroCusto && (
                  <p className="text-xs text-kyx-500">Centro de custo: {log.centroCusto}</p>
                )}
                {log.erro && <p className="text-xs text-danger-400">Erro: {log.erro}</p>}
                <div className="grid md:grid-cols-2 gap-3">
                  <div>
                    <p className="text-xs font-medium text-kyx-500 mb-1">Request</p>
                    <pre className="text-[11px] leading-relaxed font-mono text-kyx-300 bg-kyx-950/80 rounded-lg p-3 overflow-x-auto max-h-48 overflow-y-auto border border-kyx-800/40">
                      {formatJsonOrText(log.requestBody || '')}
                    </pre>
                  </div>
                  <div>
                    <p className="text-xs font-medium text-kyx-500 mb-1">Response</p>
                    <pre className="text-[11px] leading-relaxed font-mono text-kyx-300 bg-kyx-950/80 rounded-lg p-3 overflow-x-auto max-h-48 overflow-y-auto border border-kyx-800/40">
                      {log.responseBody ? formatJsonOrText(log.responseBody) : '—'}
                    </pre>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
