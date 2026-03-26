import { FormEvent, useState } from 'react';
import { notificationApi } from '../services/api';

export function EnviarNotificacaoPage() {
  const [canal, setCanal] = useState<'email' | 'sms' | 'whatsapp'>('email');
  const [centroCusto, setCentroCusto] = useState('');
  const [destinatario, setDestinatario] = useState('');
  const [assunto, setAssunto] = useState('');
  const [mensagem, setMensagem] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [ok, setOk] = useState('');

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setLoading(true);
    setError('');
    setOk('');
    try {
      const response = await notificationApi.send({
        config: { canal, centroCusto },
        dados: { destinatario, assunto, mensagem },
      });
      setOk(response.mensagem || 'Notificação enviada.');
    } catch (err: unknown) {
      const e = err as { response?: { data?: { mensagem?: string } } };
      setError(e?.response?.data?.mensagem || 'Falha ao enviar notificação.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-white">Enviar Notificação</h2>
        <p className="text-kyx-400">Envio manual de notificações (Email, SMS e WhatsApp).</p>
      </div>
      {error && <div className="p-3 rounded bg-danger-500/10 border border-danger-500/30 text-danger-500">{error}</div>}
      {ok && <div className="p-3 rounded bg-success-500/10 border border-success-500/30 text-success-500">{ok}</div>}

      <div className="card p-5 max-w-3xl">
        <form onSubmit={submit} className="space-y-3">
          <div>
            <label className="block text-xs text-kyx-500 mb-1">Canal</label>
            <select className="input" value={canal} onChange={(e) => setCanal(e.target.value as 'email' | 'sms' | 'whatsapp')}>
              <option value="email">Email</option>
              <option value="sms">SMS</option>
              <option value="whatsapp">WhatsApp</option>
            </select>
          </div>
          <div>
            <label className="block text-xs text-kyx-500 mb-1">Centro de custo</label>
            <input className="input" value={centroCusto} onChange={(e) => setCentroCusto(e.target.value)} />
          </div>
          <div>
            <label className="block text-xs text-kyx-500 mb-1">Destinatário</label>
            <input className="input" value={destinatario} onChange={(e) => setDestinatario(e.target.value)} required />
          </div>
          <div>
            <label className="block text-xs text-kyx-500 mb-1">Assunto</label>
            <input className="input" value={assunto} onChange={(e) => setAssunto(e.target.value)} />
          </div>
          <div>
            <label className="block text-xs text-kyx-500 mb-1">Mensagem</label>
            <textarea className="input min-h-[180px]" value={mensagem} onChange={(e) => setMensagem(e.target.value)} required />
          </div>
          <button className="btn btn-primary" disabled={loading} type="submit">
            {loading ? 'Enviando...' : 'Enviar notificação'}
          </button>
        </form>
      </div>
    </div>
  );
}
