import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { documentsApi, type DocumentStatusPayload } from '../services/api';

/** SDD: GET /documents/status/{jobId} — polling até completed (PDF base64) ou failed. */
export function JobsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [jobId, setJobId] = useState('');
  const [status, setStatus] = useState<DocumentStatusPayload | null>(null);
  const [loading, setLoading] = useState(false);
  const [polling, setPolling] = useState(false);
  const [error, setError] = useState('');
  const timerRef = useRef<number | null>(null);

  const clearTimer = () => {
    if (timerRef.current) {
      window.clearInterval(timerRef.current);
      timerRef.current = null;
    }
  };

  useEffect(() => {
    const q = searchParams.get('jobId');
    if (q) {
      setJobId(q);
    }
  }, [searchParams]);

  useEffect(() => () => clearTimer(), []);

  const fetchStatus = async (silent = false) => {
    if (!jobId) return;
    if (!silent) setLoading(true);
    try {
      const response = await documentsApi.getStatus(jobId);
      const nextStatus = response.resultado ?? null;
      setStatus(nextStatus);
      setError('');

      if (nextStatus?.status === 'completed' || nextStatus?.status === 'failed') {
        setPolling(false);
        clearTimer();
      }
    } catch (err: unknown) {
      const e = err as { response?: { data?: { mensagem?: string } } };
      setError(e?.response?.data?.mensagem || 'Falha ao consultar status do job.');
      setPolling(false);
      clearTimer();
    } finally {
      if (!silent) setLoading(false);
    }
  };

  const startPolling = async () => {
    if (jobId) {
      setSearchParams({ jobId });
    }
    await fetchStatus();
    setPolling(true);
    clearTimer();
    timerRef.current = window.setInterval(() => {
      fetchStatus(true);
    }, 2500);
  };

  const stopPolling = () => {
    setPolling(false);
    clearTimer();
  };

  const downloadPdf = () => {
    if (!status?.resultado?.base64) return;
    const binary = atob(status.resultado.base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i += 1) bytes[i] = binary.charCodeAt(i);
    const blob = new Blob([bytes], { type: status.resultado.contentType || 'application/pdf' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = status.resultado.nomeArquivo || 'documento.pdf';
    link.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-white">Jobs</h2>
        <p className="text-kyx-400 max-w-2xl">
          Acompanhe o processamento assíncrono (fila Hangfire). Quando <code className="text-kyx-200">status</code> for{' '}
          <strong className="text-kyx-300">completed</strong>, o resultado traz o PDF em base64 para download.
        </p>
      </div>

      {error && <div className="p-3 rounded bg-danger-500/10 border border-danger-500/30 text-danger-500">{error}</div>}

      <div className="card p-5 max-w-3xl space-y-3">
        <label className="block text-xs text-kyx-500 mb-1">jobId (GUID retornado pela geração)</label>
        <input className="input font-mono text-sm" placeholder="jobId" value={jobId} onChange={(e) => setJobId(e.target.value)} />
        <div className="flex flex-wrap gap-2">
          <button type="button" className="btn btn-primary" onClick={() => fetchStatus()} disabled={loading || !jobId}>
            Consultar
          </button>
          {!polling ? (
            <button type="button" className="btn" onClick={startPolling} disabled={!jobId}>
              Iniciar polling
            </button>
          ) : (
            <button type="button" className="btn" onClick={stopPolling}>
              Parar polling
            </button>
          )}
          <button type="button" className="btn" onClick={downloadPdf} disabled={!status?.resultado?.base64}>
            Baixar PDF
          </button>
        </div>

        {status && (
          <div className="mt-2 p-3 rounded-lg bg-kyx-900/30 border border-kyx-800/40">
            <p className="text-white">
              Status: <strong>{status.status}</strong>
            </p>
            <p className="text-kyx-400 text-sm font-mono">jobId: {status.jobId}</p>
            {status.errorMessage && <p className="text-danger-500 text-sm mt-1">{status.errorMessage}</p>}
            {status.resultado && <p className="text-kyx-300 text-sm mt-1">Arquivo: {status.resultado.nomeArquivo}</p>}
          </div>
        )}
      </div>
    </div>
  );
}
