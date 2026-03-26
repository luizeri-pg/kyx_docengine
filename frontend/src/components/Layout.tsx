import { Outlet, Link, useLocation, useNavigate } from 'react-router-dom';
import { 
  FileText,
  LayoutDashboard,
  FilePlus2,
  History,
  FileSearch,
  Users,
  Cog,
  LogOut, 
  Menu,
  X,
} from 'lucide-react';
import { useState } from 'react';
import { useAuthStore } from '../stores/authStore';
import clsx from 'clsx';

const navigation = [
  { name: 'Dashboard', href: '/dashboard', icon: LayoutDashboard },
  { name: 'Gerar Documento', href: '/generate', icon: FilePlus2 },
  { name: 'Histórico', href: '/historico', icon: History },
  { name: 'Auditoria', href: '/auditoria', icon: FileSearch },
  { name: 'Usuários', href: '/users', icon: Users },
  { name: 'Integrações', href: '/integracoes', icon: Cog },
];

export function Layout() {
  const location = useLocation();
  const navigate = useNavigate();
  const { user, logout } = useAuthStore();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="min-h-screen bg-kyx-950">
      {/* Background Pattern */}
      <div className="fixed inset-0 bg-grid-pattern opacity-30 pointer-events-none" />
      <div className="fixed inset-0 bg-gradient-to-br from-kyx-900/50 via-transparent to-kyx-950/80 pointer-events-none" />

      {/* Mobile sidebar toggle */}
      <button
        onClick={() => setSidebarOpen(!sidebarOpen)}
        className="lg:hidden fixed top-4 left-4 z-50 p-2 rounded-lg bg-kyx-800/80 text-white"
      >
        {sidebarOpen ? <X size={24} /> : <Menu size={24} />}
      </button>

      {/* Sidebar */}
      <aside
        className={clsx(
          'fixed inset-y-0 left-0 z-40 w-64 transform transition-transform duration-300 lg:translate-x-0',
          sidebarOpen ? 'translate-x-0' : '-translate-x-full'
        )}
      >
        <div className="h-full flex flex-col bg-kyx-950/95 backdrop-blur-xl border-r border-kyx-800/30">
          {/* Logo */}
          <div className="flex items-center gap-3 px-6 py-5 border-b border-kyx-800/30">
            <div className="p-2 rounded-xl bg-gradient-to-br from-kyx-600/60 to-kyx-700/80 shadow-lg shadow-kyx-600/10">
              <FileText className="w-6 h-6 text-white" />
            </div>
            <div>
              <h1 className="text-lg font-bold text-white">KYX - DocEngine</h1>
              <p className="text-xs text-kyx-400">Painel Administrativo</p>
            </div>
          </div>

          {/* Navigation */}
          <nav className="flex-1 px-3 py-4 space-y-1">
            {navigation.map((item) => {
              const isActive = location.pathname === item.href;
              return (
                <Link
                  key={item.name}
                  to={item.href}
                  onClick={() => setSidebarOpen(false)}
                  className={clsx(
                    'flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200',
                    isActive
                      ? 'bg-kyx-700/40 text-white shadow-lg shadow-kyx-700/20'
                      : 'text-kyx-400 hover:text-white hover:bg-kyx-800/50'
                  )}
                >
                  <item.icon size={20} />
                  <span className="font-medium">{item.name}</span>
                </Link>
              );
            })}
          </nav>

          {/* User section */}
          <div className="p-4 border-t border-kyx-800/30">
            <div className="flex items-center gap-3 px-3 py-2 rounded-xl bg-kyx-900/50">
              <div className="w-10 h-10 rounded-full bg-gradient-to-br from-kyx-500/60 to-kyx-600/80 flex items-center justify-center">
                <span className="text-white font-bold">
                  {user?.nome?.charAt(0).toUpperCase() || 'U'}
                </span>
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-white truncate">
                  {user?.nome || 'Usuário'}
                </p>
                <p className="text-xs text-kyx-400 truncate">
                  {user?.perfil || 'Perfil'}
                </p>
              </div>
              <button
                onClick={handleLogout}
                className="p-2 rounded-lg text-kyx-400 hover:text-white hover:bg-kyx-800/50 transition-colors"
                title="Sair"
              >
                <LogOut size={18} />
              </button>
            </div>
          </div>
        </div>
      </aside>

      {/* Main content */}
      <main className="lg:pl-64 min-h-screen relative">
        <div className="p-6 lg:p-8">
          <Outlet />
        </div>
      </main>

      {/* Mobile overlay */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 bg-black/50 z-30 lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}
    </div>
  );
}

