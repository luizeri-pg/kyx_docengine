import { FormEvent, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  documentsApi,
  templatesApi,
  type GenerateDocumentApiResponse,
  type TemplateResponse,
} from '../services/api';
import { parseDadosJsonForApi } from '../utils/flattenDados';

const EXEMPLO_DADOS_ANINHADOS = `{
  "guiaNumero": "19368736",
  "guiaDataEmissao": "18/11/2025",
  "empresa": {
    "nome": "IDEMIA IDENTITY & SECURITY FRANCE PARA O BRASIL",
    "unidade": "IDEMIA IDENTITY & SECURITY",
    "cnpj": "44.699.235/0001-99"
  },
  "funcionario": {
    "nome": "ALESSANDRO OSVALDIR CARVALHO",
    "cpf": "131.304.658-20"
  },
  "exames": [
    { "tuss": "10101012", "nome": "Avaliação clínica" }
  ]
}`;

/** POST /documents/generate — dados planos ou JSON aninhado (achatado para a API). Resposta: envelope ApiResponse. */
export function GenerateDocumentPage() {
  const navigate = useNavigate();
  const [templates, setTemplates] = useState<TemplateResponse[]>([]);
  const [requisicaoId, setRequisicaoId] = useState<string>(() => crypto.randomUUID());
  const [templateSlug, setTemplateSlug] = useState('');
  const [centroCusto, setCentroCusto] = useState('');
  const [nomeArquivo, setNomeArquivo] = useState('documento.pdf');
  const [dadosText, setDadosText] = useState(
    '{\n  "nome": "Fulano",\n  "cpf": "00000000000"\n}'
  );
  const [dadosFlattenPreview, setDadosFlattenPreview] = useState<string | null>(null);
  const [lastResponse, setLastResponse] = useState<GenerateDocumentApiResponse | null>(null);
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
    setLastResponse(null);
    setDadosFlattenPreview(null);
    setLoading(true);
    try {
      let dados: Record<string, string>;
      try {
        dados = parseDadosJsonForApi(dadosText);
        setDadosFlattenPreview(JSON.stringify(dados, null, 2));
      } catch {
        setError('JSON de dados inválido (sintaxe ou raiz precisa ser um objeto).');
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
      setLastResponse(response);
      const jid = response.resultado?.jobId || '';
      setJobId(jid);
      setStatus(response.resultado?.status || 'queued');
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
          Corpo alinhado ao backend: <code className="text-kyx-200">requisicaoId</code>,{' '}
          <code className="text-kyx-200">config</code> (template, centroCusto, nomeArquivo),{' '}
          <code className="text-kyx-200">dados</code>. Objetos e listas no JSON são{' '}
          <strong className="text-kyx-300">achatados</strong> para{' '}
          <code className="text-kyx-200">Record&lt;string, string&gt;</code> (ex.:{' '}
          <code className="text-kyx-200">empresa.nome</code>, <code className="text-kyx-200">exames.0.tuss</code>) — use
          as mesmas chaves no HTML do template.
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
            <div className="flex flex-wrap items-center justify-between gap-2 mb-1">
              <label className="block text-xs text-kyx-500">dados (JSON — plano ou aninhado)</label>
              <button
                type="button"
                className="btn text-xs"
                onClick={() => setDadosText(EXEMPLO_DADOS_ANINHADOS)}
              >
                Carregar exemplo aninhado
              </button>
            </div>
            <textarea className="input min-h-[220px] font-mono text-sm" value={dadosText} onChange={(e) => setDadosText(e.target.value)} required />
          </div>
          <button className="btn btn-primary" type="submit" disabled={loading || loadingTemplates}>
            {loading ? 'Enfileirando…' : 'POST /documents/generate'}
          </button>
        </form>

        {dadosFlattenPreview && (
          <div className="p-3 rounded-lg border border-kyx-800/40 bg-kyx-950/50">
            <p className="text-xs text-kyx-400 mb-1">Payload enviado em <code className="text-kyx-300">dados</code> (após achatamento)</p>
            <pre className="text-xs text-kyx-300 font-mono whitespace-pre-wrap break-all max-h-40 overflow-auto">{dadosFlattenPreview}</pre>
          </div>
        )}

        {lastResponse && (
          <div className="p-3 rounded-lg border border-kyx-700/50 bg-kyx-900/40 space-y-2">
            <p className="text-sm font-semibold text-white">Resposta da API (envelope)</p>
            <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-4 gap-y-1 text-xs">
              <dt className="text-kyx-500">sucesso</dt>
              <dd className="text-kyx-200 font-mono">{String(lastResponse.sucesso)}</dd>
              <dt className="text-kyx-500">mensagem</dt>
              <dd className="text-kyx-200 font-mono break-all">{lastResponse.mensagem ?? '—'}</dd>
              <dt className="text-kyx-500">tempoProcessamento (ms)</dt>
              <dd className="text-kyx-200 font-mono">{lastResponse.tempoProcessamento}</dd>
              <dt className="text-kyx-500">requisicaoId</dt>
              <dd className="text-kyx-200 font-mono break-all">{lastResponse.requisicaoId}</dd>
              <dt className="text-kyx-500">resultado.jobId</dt>
              <dd className="text-kyx-200 font-mono break-all">{lastResponse.resultado?.jobId ?? '—'}</dd>
              <dt className="text-kyx-500">resultado.status</dt>
              <dd className="text-kyx-200 font-mono">{lastResponse.resultado?.status ?? '—'}</dd>
            </dl>
          </div>
        )}

        {jobId && (
          <div className="p-3 rounded-lg bg-kyx-900/30 border border-kyx-800/40">
            <p className="text-white">
              Job: <span className="font-mono">{jobId}</span>
            </p>
            <p className="text-kyx-400 text-sm">Status: {status}</p>
            <button type="button" className="btn btn-primary mt-2 text-sm" onClick={() => navigate(`/jobs?jobId=${encodeURIComponent(jobId)}`)}>
              Ir para Jobs — polling e download do PDF
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
