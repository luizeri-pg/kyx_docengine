import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Calendar,
  ChevronDown,
  ChevronUp,
  Clock,
  FileText,
  History,
  RefreshCw,
  Search,
  CheckCircle2,
  XCircle,
  Loader2,
  AlertCircle,
} from 'lucide-react';
import clsx from 'clsx';
import { documentsApi, type DocumentJobListItem } from '../services/api';

function statusLabel(status: string) {
  switch (status) {
    case 'completed':
      return 'Concluído';
    case 'failed':
      return 'Erro';
    case 'processing':
      return 'Processando';
    case 'pending':
      return 'Pendente';
    default:
      return status;
  }
}

function StatusIcon({ status }: { status: string }) {
  if (status === 'completed')
    return <CheckCircle2 className="w-6 h-6 text-success-500 shrink-0" aria-hidden />;
  if (status === 'failed')
    return <XCircle className="w-6 h-6 text-danger-500 shrink-0" aria-hidden />;
  if (status === 'processing')
    return <Loader2 className="w-6 h-6 text-warning-500 animate-spin shrink-0" aria-hidden />;
  return <AlertCircle className="w-6 h-6 text-kyx-400 shrink-0" aria-hidden />;
}

/** Layout referência: histórico em cards — adaptado ao DocEngine (jobs de PDF). */
export function HistoricoPage() {
  const [items, setItems] = useState<DocumentJobListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [templateType, setTemplateType] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const [debouncedSearch, setDebouncedSearch] = useState('');

  useEffect(() => {
    const id = window.setTimeout(() => setDebouncedSearch(search.trim()), 450);
    return () => window.clearTimeout(id);
  }, [search]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const res = await documentsApi.listJobs({
        limit: 200,
        status: statusFilter === 'all' ? undefined : statusFilter,
        templateType: templateType === 'all' ? undefined : templateType,
        search: debouncedSearch || undefined,
      });
      setItems(res.resultado ?? []);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { mensagem?: string } } };
      setError(e?.response?.data?.mensagem || 'Não foi possível carregar o histórico.');
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [debouncedSearch, statusFilter, templateType]);

  useEffect(() => {
    load();
  }, [load]);

  const stats = useMemo(() => {
    const total = items.length;
    const sucesso = items.filter((j) => j.status === 'completed').length;
    const erros = items.filter((j) => j.status === 'failed').length;
    return { total, sucesso, erros };
  }, [items]);

  const toggle = (id: string) => {
    setExpanded((p) => ({ ...p, [id]: !p[id] }));
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex gap-3">
          <div className="mt-1 flex h-11 w-11 items-center justify-center rounded-full bg-warning-600/20 text-warning-500">
            <History className="h-6 w-6" />
          </div>
          <div>
            <h2 className="text-2xl font-bold text-warning-500">Histórico de documentos</h2>
            <p className="text-kyx-400 mt-1 max-w-2xl text-sm">
              Visualize todas as gerações de PDF com detalhes completos (Request ID, template e tempos).
            </p>
          </div>
        </div>
        <button
          type="button"
          onClick={() => load()}
          disabled={loading}
          className="inline-flex items-center gap-2 self-start rounded-lg border border-kyx-700/50 bg-kyx-900/50 px-4 py-2.5 text-sm font-medium text-kyx-200 hover:bg-kyx-800/50 hover:text-white disabled:opacity-50"
        >
          <RefreshCw className={clsx('h-4 w-4', loading && 'animate-spin')} />
          Atualizar
        </button>
      </div>

      {error && (
        <div className="rounded-lg border border-danger-500/30 bg-danger-500/10 p-3 text-sm text-danger-500">
          {error}
        </div>
      )}

      <div className="card space-y-4 p-5 md:p-6">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-kyx-500" />
          <input
            className="input w-full pl-10"
            placeholder="Buscar por Request ID, template, arquivo ou centro de custo..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && (e.preventDefault(), load())}
          />
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <div>
            <label className="mb-1 block text-xs text-kyx-500">Tipo de template</label>
            <select
              className="input"
              value={templateType}
              onChange={(e) => setTemplateType(e.target.value)}
            >
              <option value="all">Todos os tipos</option>
              <option value="html">html</option>
              <option value="acroform">acroform</option>
            </select>
          </div>
          <div>
            <label className="mb-1 block text-xs text-kyx-500">Status</label>
            <select className="input" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
              <option value="all">Todos os status</option>
              <option value="pending">Pendente</option>
              <option value="processing">Processando</option>
              <option value="completed">Concluído</option>
              <option value="failed">Erro</option>
            </select>
          </div>
        </div>
        <div className="flex flex-wrap gap-6 border-t border-kyx-800/40 pt-4 text-sm">
          <span>
            <span className="text-kyx-500">Total:</span>{' '}
            <span className="font-semibold text-white">{loading ? '…' : stats.total}</span>
          </span>
          <span>
            <span className="text-kyx-500">Sucesso:</span>{' '}
            <span className="font-semibold text-success-500">{loading ? '…' : stats.sucesso}</span>
          </span>
          <span>
            <span className="text-kyx-500">Erros:</span>{' '}
            <span className="font-semibold text-danger-500">{loading ? '…' : stats.erros}</span>
          </span>
        </div>
      </div>

      <div className="space-y-3">
        {loading && items.length === 0 && (
          <div className="card p-12 text-center text-kyx-400">Carregando histórico…</div>
        )}

        {!loading && items.length === 0 && (
          <div className="card p-12 text-center text-kyx-500">Nenhum job encontrado com os filtros atuais.</div>
        )}

        {items.map((job) => {
          const open = expanded[job.jobId];
          return (
            <div
              key={job.jobId}
              className="rounded-xl border border-kyx-800/40 bg-kyx-950/40 p-4 transition-colors hover:border-kyx-700/50"
            >
              <button
                type="button"
                onClick={() => toggle(job.jobId)}
                className="flex w-full items-start gap-3 text-left"
              >
                <StatusIcon status={job.status} />
                <div className="min-w-0 flex-1 space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-mono text-sm text-white break-all">{job.requisicaoId}</span>
                    <span className="badge bg-warning-500/15 text-warning-600/90 capitalize">{job.templateType}</span>
                    <span className="badge bg-kyx-800/80 text-kyx-200 max-w-[200px] truncate" title={job.templateName}>
                      {job.templateName || job.templateSlug}
                    </span>
                  </div>
                  <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-kyx-400">
                    <span className="inline-flex items-center gap-1.5">
                      <FileText className="h-3.5 w-3.5 shrink-0" />
                      {job.nomeArquivo}
                    </span>
                    <span className="inline-flex items-center gap-1.5">
                      <Calendar className="h-3.5 w-3.5 shrink-0" />
                      {new Date(job.createdAt).toLocaleString('pt-BR')}
                    </span>
                    {job.processingTimeMs != null && (
                      <span className="inline-flex items-center gap-1.5">
                        <Clock className="h-3.5 w-3.5 shrink-0" />
                        {job.processingTimeMs}ms
                      </span>
                    )}
                  </div>
                  {open && (
                    <div className="pt-2 text-xs text-kyx-500 border-t border-kyx-800/30 mt-2 space-y-1">
                      <p>
                        <span className="text-kyx-500">Job ID:</span>{' '}
                        <span className="font-mono text-kyx-300">{job.jobId}</span>
                      </p>
                      <p>
                        <span className="text-kyx-500">Centro de custo:</span> {job.centroCusto}
                      </p>
                      <p>
                        <span className="text-kyx-500">Slug:</span> {job.templateSlug}
                      </p>
                      {job.errorMessage && (
                        <p className="text-danger-400 mt-2">{job.errorMessage}</p>
                      )}
                    </div>
                  )}
                </div>
                <div className="flex flex-col items-end gap-2 shrink-0">
                  <span
                    className={clsx(
                      'text-xs font-semibold',
                      job.status === 'completed' && 'text-success-500',
                      job.status === 'failed' && 'text-danger-500',
                      job.status !== 'completed' && job.status !== 'failed' && 'text-warning-500'
                    )}
                  >
                    {statusLabel(job.status)}
                  </span>
                  {open ? (
                    <ChevronUp className="h-5 w-5 text-kyx-500" />
                  ) : (
                    <ChevronDown className="h-5 w-5 text-kyx-500" />
                  )}
                </div>
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
}
