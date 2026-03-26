import { useState } from 'react';

export function NotifyIntegracoesPage() {
  const [canal, setCanal] = useState('');

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-white">Integrações</h2>
        <p className="text-kyx-400">Consulta das integrações configuradas.</p>
      </div>
      <div className="card p-5">
        <div className="grid md:grid-cols-3 gap-3 items-end">
          <div>
            <label className="block text-xs text-kyx-500 mb-1">Canal</label>
            <input className="input" placeholder="email/sms/whatsapp" value={canal} onChange={(e) => setCanal(e.target.value)} />
          </div>
          <p className="text-sm text-kyx-400">Módulo visual legado (sem API no DocEngine).</p>
        </div>
      </div>
      <div className="card p-5">
        <h3 className="text-lg font-semibold text-white mb-3">Lista de integrações</h3>
        <p className="text-kyx-500">Nenhuma integração carregada.</p>
      </div>
    </div>
  );
}
