export function NotifyDashboardPage() {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-white">Dashboard</h2>
        <p className="text-kyx-400">Visão geral do painel administrativo.</p>
      </div>

      <div className="grid md:grid-cols-3 gap-4">
        <div className="card p-4"><p className="text-kyx-400 text-sm">Total</p><p className="text-white text-2xl font-bold">-</p></div>
        <div className="card p-4"><p className="text-kyx-400 text-sm">Sucesso</p><p className="text-success-500 text-2xl font-bold">-</p></div>
        <div className="card p-4"><p className="text-kyx-400 text-sm">Erros</p><p className="text-danger-500 text-2xl font-bold">-</p></div>
      </div>
      <div className="card p-5 text-kyx-400">
        Backend DocEngine ativo. Módulos administrativos legados estão em modo visual sem chamada de API.
      </div>
    </div>
  );
}
