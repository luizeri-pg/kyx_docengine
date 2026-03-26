import { FormEvent, useState } from 'react';
import { logsApi, type LogDetalhe, type LogRequisicao } from '../services/api';

export function LogsPage() {
  const [items, setItems] = useState<LogRequisicao[]>([]);
  const [detalhe, setDetalhe] = useState<LogDetalhe | null>(null);
  const [canal, setCanal] = useState('');
  const [centroCusto, setCentroCusto] = useState('');
  const [limit, setLimit] = useState(50);
  const [loading, setLoading] = useState(false);
  const [loadingDetalhe, setLoadingDetalhe] = useState(false);
  const [error, setError] = useState('');

  const buscar = async (event?: FormEvent) => {
    event?.preventDefault();
    setLoading(true);
    setError('');
    try {
      const res = await logsApi.history({
        canal: canal || undefined,
        centroCusto: centroCusto || undefined,
        limit,
        offset: 0,
      });
      setItems(res.resultado ?? []);
      setDetalhe(null);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { mensagem?: string } } };
      setError(e?.response?.data?.mensagem || 'Falha ao consultar logs.');
    } finally {
      setLoading(false);
    }
  };

  const abrirDetalhe = async (requisicaoId: string) => {
    setLoadingDetalhe(true);
    setError('');
    try {
      const res = await logsApi.detalhes(requisicaoId);
      setDetalhe(res.resultado ?? null);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { mensagem?: string } } };
      setError(e?.response?.data?.mensagem || 'Falha ao carregar detalhe da requisição.');
      setDetalhe(null);
    } finally {
      setLoadingDetalhe(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-white">Consulta de Logs</h2>
        <p className="text-kyx-400 max-w-2xl">Auditoria e troubleshooting das requisições processadas.</p>
      </div>

      {error && <div className="p-3 rounded bg-danger-500/10 border border-danger-500/30 text-danger-500">{error}</div>}

      <div className="card p-5">
        <form onSubmit={buscar} className="grid md:grid-cols-4 gap-3 items-end">
          <div>
            <label className="block text-xs text-kyx-500 mb-1">Canal</label>
            <input className="input" placeholder="email/sms/whatsapp" value={canal} onChange={(e) => setCanal(e.target.value)} />
          </div>
          <div>
            <label className="block text-xs text-kyx-500 mb-1">Centro de custo</label>
            <input className="input" placeholder="ex: sfairalimentos" value={centroCusto} onChange={(e) => setCentroCusto(e.target.value)} />
          </div>
          <div>
            <label className="block text-xs text-kyx-500 mb-1">Limite</label>
            <input
              className="input"
              type="number"
              min={1}
              max={500}
              value={limit}
              onChange={(e) => setLimit(Math.max(1, Number(e.target.value) || 1))}
            />
          </div>
          <button className="btn btn-primary" type="submit" disabled={loading}>
            {loading ? 'Consultando...' : 'Consultar'}
          </button>
        </form>
      </div>

      <div className="grid lg:grid-cols-2 gap-6">
        <div className="card p-5">
          <h3 className="text-lg font-semibold text-white mb-3">Histórico</h3>
          <div className="space-y-3 max-h-[560px] overflow-auto pr-1">
            {items.map((item) => (
              <button
                key={item.id}
                type="button"
                onClick={() => abrirDetalhe(item.requisicaoId)}
                className="w-full text-left p-3 rounded-lg border border-kyx-800/40 bg-kyx-900/30 hover:bg-kyx-900/50"
              >
                <p className="text-white font-medium font-mono text-sm">{item.requisicaoId}</p>
                <p className="text-xs text-kyx-400">
                  Canal: {item.canal || '-'} • HTTP: {item.statusHttp ?? '-'} • Centro de custo: {item.centroCusto || '-'}
                </p>
                <p className="text-xs text-kyx-500 mt-1">{new Date(item.criadoEm).toLocaleString('pt-BR')}</p>
              </button>
            ))}
            {!items.length && <p className="text-kyx-500">Nenhum registro encontrado.</p>}
          </div>
        </div>

        <div className="card p-5">
          <h3 className="text-lg font-semibold text-white mb-3">Detalhes</h3>
          {loadingDetalhe && <p className="text-kyx-400">Carregando detalhe...</p>}
          {!loadingDetalhe && !detalhe && <p className="text-kyx-500">Selecione uma requisição no histórico.</p>}
          {!loadingDetalhe && detalhe && (
            <div className="space-y-3">
              <div className="p-3 rounded-lg border border-kyx-800/40 bg-kyx-900/30">
                <p className="text-white font-mono text-sm">{detalhe.requisicao.requisicaoId}</p>
                <p className="text-xs text-kyx-400">
                  HTTP: {detalhe.requisicao.statusHttp ?? '-'} • Tempo: {detalhe.requisicao.tempoRespostaMs ?? '-'} ms
                </p>
                {detalhe.requisicao.erro && <p className="text-xs text-danger-500 mt-1">Erro: {detalhe.requisicao.erro}</p>}
              </div>

              <div className="space-y-2">
                <p className="text-sm text-kyx-300">Logs de integração ({detalhe.integracoes.length})</p>
                {detalhe.integracoes.map((log) => (
                  <div key={log.id} className="p-3 rounded-lg border border-kyx-800/40 bg-kyx-900/30 text-xs">
                    <p className="text-white">{log.metodo || '-'} {log.endpoint || '-'}</p>
                    <p className="text-kyx-400">HTTP {log.statusHttp ?? '-'} • {log.tempoRespostaMs ?? '-'} ms</p>
                  </div>
                ))}
                {!detalhe.integracoes.length && <p className="text-kyx-500 text-sm">Sem logs de integração vinculados.</p>}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

