import { FormEvent, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { documentsApi, templatesApi, type TemplateResponse } from '../services/api';

/** Alinhado ao SDD: POST /documents/generate com requisicaoId, config (template slug, centroCusto, nomeArquivo), dados. */
export function GenerateDocumentPage() {
  const navigate = useNavigate();
  const [templates, setTemplates] = useState<TemplateResponse[]>([]);
  const [requisicaoId, setRequisicaoId] = useState<string>(() => crypto.randomUUID());
  const [templateSlug, setTemplateSlug] = useState('');
  const [centroCusto, setCentroCusto] = useState('');
  const [nomeArquivo, setNomeArquivo] = useState('documento.pdf');
  const [dadosText, setDadosText] = useState('{\n  "nome": "Fulano",\n  "cpf": "00000000000"\n}');
  const [jobId, setJobId] = useState('');
  const [status, setStatus] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [loadingTemplates, setLoadingTemplates] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const res = await templatesApi.list();
        setTemplates(res.resultado ?? []);
      } catch {
        setTemplates([]);
      } finally {
        setLoadingTemplates(false);
      }
    })();
  }, []);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError('');
    setLoading(true);
    try {
      let dados: Record<string, string>;
      try {
        const parsed = JSON.parse(dadosText) as Record<string, unknown>;
        dados = Object.fromEntries(
          Object.entries(parsed).map(([k, v]) => [k, v === null || v === undefined ? '' : String(v)])
        );
      } catch {
        setError('JSON de dados inválido.');
        setLoading(false);
        return;
      }

      const response = await documentsApi.generate({
        requisicaoId,
        config: {
          template: templateSlug,
          centroCusto,
          nomeArquivo,
        },
        dados,
      });
      const jid = response.resultado?.jobId || '';
      setJobId(jid);
      setStatus(response.resultado?.status || 'queued');
      if (jid) {
        navigate(`/jobs?jobId=${encodeURIComponent(jid)}`);
      }
    } catch (err: unknown) {
      const e = err as { response?: { data?: { mensagem?: string } } };
      setError(e?.response?.data?.mensagem || 'Falha ao gerar documento.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-white">Gerar documento</h2>
        <p className="text-kyx-400 max-w-2xl">
          Enfileira um job de geração de PDF conforme SDD: <code className="text-kyx-200">requisicaoId</code>,{' '}
          <code className="text-kyx-200">config.template</code> (slug), <code className="text-kyx-200">centroCusto</code>,{' '}
          <code className="text-kyx-200">dados</code> (chaves = campos do template).
        </p>
      </div>

      {error && <div className="p-3 rounded bg-danger-500/10 border border-danger-500/30 text-danger-500">{error}</div>}

      <div className="card p-5 max-w-3xl space-y-4">
        <form onSubmit={submit} className="space-y-3">
          <div>
            <label className="block text-xs text-kyx-500 mb-1">requisicaoId (rastreio / auditoria)</label>
            <input className="input font-mono text-sm" value={requisicaoId} onChange={(e) => setRequisicaoId(e.target.value)} required />
          </div>
          <div>
            <label className="block text-xs text-kyx-500 mb-1">config.template — slug do template</label>
            {loadingTemplates ? (
              <p className="text-kyx-500 text-sm">Carregando templates…</p>
            ) : (
              <select
                className="input"
                value={templateSlug}
                onChange={(e) => setTemplateSlug(e.target.value)}
                required
              >
                <option value="">Selecione um template</option>
                {templates.map((t) => (
                  <option key={t.id} value={t.slug}>
                    {t.name} ({t.slug}) — {t.type}
                  </option>
                ))}
              </select>
            )}
          </div>
          <div>
            <label className="block text-xs text-kyx-500 mb-1">config.centroCusto</label>
            <input className="input" placeholder="ex: sfairalimentos" value={centroCusto} onChange={(e) => setCentroCusto(e.target.value)} required />
          </div>
          <div>
            <label className="block text-xs text-kyx-500 mb-1">config.nomeArquivo</label>
            <input className="input" value={nomeArquivo} onChange={(e) => setNomeArquivo(e.target.value)} />
          </div>
          <div>
            <label className="block text-xs text-kyx-500 mb-1">dados (JSON — valores string)</label>
            <textarea className="input min-h-[220px] font-mono text-sm" value={dadosText} onChange={(e) => setDadosText(e.target.value)} required />
          </div>
          <button className="btn btn-primary" type="submit" disabled={loading || loadingTemplates}>
            {loading ? 'Enfileirando…' : 'POST /documents/generate'}
          </button>
        </form>

        {jobId && (
          <div className="p-3 rounded-lg bg-kyx-900/30 border border-kyx-800/40">
            <p className="text-white">
              Job: <span className="font-mono">{jobId}</span>
            </p>
            <p className="text-kyx-400 text-sm">Status: {status}</p>
            <button type="button" className="btn btn-primary mt-2 text-sm" onClick={() => navigate(`/jobs?jobId=${encodeURIComponent(jobId)}`)}>
              Abrir polling em Jobs
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
