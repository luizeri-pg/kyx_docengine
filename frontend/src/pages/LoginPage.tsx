import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Eye, EyeOff, LogIn, AlertCircle, FileText } from 'lucide-react';
import { useAuthStore } from '../stores/authStore';
import { API_BASE_URL, authApi } from '../services/api';
import { decodeJwtPayload } from '../utils/jwt';

export function LoginPage() {
  const navigate = useNavigate();
  const login = useAuthStore((state) => state.login);
  
  // Usa 'username' para aceitar tanto email quanto nome de usuário
  const [username, setUsername] = useState('');
  const [senha, setSenha] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      const response = await authApi.login(username, senha);
      
      if (response.sucesso && response.resultado) {
        const token = response.resultado.access_token;
        const payload = decodeJwtPayload(token);
        const roleClaim =
          (payload.role as string | undefined) ||
          (payload[
            'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
          ] as string | undefined) ||
          'admin';
        const roles = String(roleClaim)
          .split(',')
          .map((r: string) => r.trim().toLowerCase())
          .filter(Boolean);
        const nameIdentifier =
          (payload.nameid as string | undefined) ||
          (payload[
            'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'
          ] as string | undefined) ||
          username;
        const displayName =
          (payload.unique_name as string | undefined) ||
          (payload.name as string | undefined) ||
          (payload[
            'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'
          ] as string | undefined) ||
          username;
        
        login(token, {
          id: nameIdentifier,
          nome: displayName,
          email: displayName,
          perfil: roles.includes('admin') ? 'Administrador' : 'Operador',
          permissoes: roles
        });
        navigate('/templates');
      } else {
        setError(response.mensagem || 'Erro ao fazer login');
      }
    } catch (err: any) {
      // Sem resposta HTTP = API offline, URL errada ou bloqueio de rede
      const isOffline =
        err.code === 'ERR_NETWORK' ||
        err.code === 'ECONNREFUSED' ||
        err.message === 'Network Error' ||
        (!err.response && err.request);
      const errorMessage = isOffline
        ? `Não foi possível ligar à API${
            API_BASE_URL === '/api' ? ' (proxy /api → localhost:3000)' : ` em ${API_BASE_URL}`
          }. Inicie o backend: na pasta backend/KYX.DocEngine.API execute «dotnet run» (porta 3000 por defeito).`
        : err.response?.data?.mensagem || err.message || 'Credenciais inválidas ou servidor indisponível';
      setError(errorMessage);
      console.error('Erro no login:', err);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-kyx-950 relative overflow-hidden">
      {/* Background Effects */}
      <div className="absolute inset-0 bg-grid-pattern opacity-20" />
      <div className="absolute inset-0 bg-gradient-radial from-kyx-900/30 via-transparent to-transparent" />

      {/* Animated orbs */}
      <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-kyx-600/20 rounded-full blur-3xl animate-pulse-subtle" />
      <div className="absolute bottom-1/4 right-1/4 w-80 h-80 bg-kyx-500/10 rounded-full blur-3xl animate-pulse-subtle" style={{ animationDelay: '1s' }} />

      {/* Login Card */}
      <div className="relative z-10 w-full max-w-md mx-4">
        <div className="card backdrop-blur-xl bg-kyx-950/80 border-kyx-700/30 p-8 animate-slide-up">
          {/* Logo */}
          <div className="text-center mb-8">
            <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-gradient-to-br from-kyx-500 to-kyx-700 shadow-lg shadow-kyx-500/30 mb-4">
              <FileText className="w-8 h-8 text-white" />
            </div>
            <h1 className="text-2xl font-bold text-white">KYX - DocEngine</h1>
            <p className="text-kyx-400 mt-1">Painel Administrativo</p>
          </div>

          {/* Error Message */}
          {error && (
            <div className="mb-6 p-4 rounded-lg bg-danger-500/10 border border-danger-500/30 flex items-center gap-3 animate-fade-in">
              <AlertCircle className="w-5 h-5 text-danger-500 flex-shrink-0" />
              <p className="text-sm text-danger-500">{error}</p>
            </div>
          )}

          {/* Form */}
          <form onSubmit={handleSubmit} className="space-y-5">
            <div>
              <label className="block text-sm font-medium text-kyx-300 mb-2">
                Usuário ou Email
              </label>
              <input
                type="text"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                className="input"
                placeholder="admin ou admin@docengine.com"
                required
                autoComplete="username"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-kyx-300 mb-2">
                Senha
              </label>
              <div className="relative">
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={senha}
                  onChange={(e) => setSenha(e.target.value)}
                  className="input pr-12"
                  placeholder="••••••••"
                  required
                  autoComplete="current-password"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-kyx-400 hover:text-white transition-colors"
                >
                  {showPassword ? <EyeOff size={20} /> : <Eye size={20} />}
                </button>
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full btn btn-primary py-3 flex items-center justify-center gap-2 mt-6"
            >
              {loading ? (
                <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              ) : (
                <>
                  <LogIn size={20} />
                  Entrar
                </>
              )}
            </button>
          </form>

          {/* Footer */}
          <div className="mt-8 pt-6 border-t border-kyx-800/30 text-center">
            <p className="text-xs text-kyx-500">
              Acesso restrito via VPN • © 2026 KYX
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}


