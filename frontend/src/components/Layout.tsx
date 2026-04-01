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
  ChevronLeft,
  ChevronRight,
} from 'lucide-react';
import { useState, useEffect } from 'react';
import { useAuthStore } from '../stores/authStore';
import clsx from 'clsx';

const SIDEBAR_EXPANDED_KEY = 'kyx-sidebar-expanded';

function readSidebarExpanded(): boolean {
  try {
    const v = localStorage.getItem(SIDEBAR_EXPANDED_KEY);
    if (v === 'false') return false;
    if (v === 'true') return true;
  } catch {
    /* ignore */
  }
  return true;
}

const navigation = [
  { name: 'Dashboard', href: '/dashboard', icon: LayoutDashboard },
  { name: 'Gerar Documento', href: '/generate', icon: FilePlus2 },
  { name: 'Modelos/Formularios', href: '/templates', icon: FileText },
  { name: 'Histórico', href: '/historico', icon: History },
  { name: 'Auditoria', href: '/auditoria', icon: FileSearch },
  { name: 'Usuários', href: '/users', icon: Users },
  { name: 'Integrações', href: '/integracoes', icon: Cog },
];

export function Layout() {
  const location = useLocation();
  const navigate = useNavigate();
  const { user, logout } = useAuthStore();
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);
  /** Desktop: expandida (texto + ícones) vs rail estreita (só ícones) */
  const [desktopExpanded, setDesktopExpanded] = useState(readSidebarExpanded);

  useEffect(() => {
    try {
      localStorage.setItem(SIDEBAR_EXPANDED_KEY, desktopExpanded ? 'true' : 'false');
    } catch {
      /* ignore */
    }
  }, [desktopExpanded]);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="min-h-screen bg-kyx-950">
      <div className="fixed inset-0 bg-grid-pattern opacity-30 pointer-events-none" />
      <div className="fixed inset-0 bg-gradient-to-br from-kyx-900/50 via-transparent to-kyx-950/80 pointer-events-none" />

      <button
        type="button"
        onClick={() => setMobileSidebarOpen(!mobileSidebarOpen)}
        className="lg:hidden fixed top-4 left-4 z-50 p-2 rounded-lg bg-kyx-800/80 text-white"
        aria-label={mobileSidebarOpen ? 'Fechar menu' : 'Abrir menu'}
      >
        {mobileSidebarOpen ? <X size={24} /> : <Menu size={24} />}
      </button>

      <aside
        className={clsx(
          'fixed inset-y-0 left-0 z-40 w-64 overflow-visible',
          'transform transition-[width,transform] duration-300 ease-out',
          'bg-kyx-950/95 backdrop-blur-xl border-r border-kyx-800/30',
          mobileSidebarOpen ? 'translate-x-0' : '-translate-x-full',
          'lg:translate-x-0',
          desktopExpanded ? 'lg:w-64' : 'lg:w-20'
        )}
      >
        <div className="h-full flex flex-col min-h-0 overflow-hidden">
          <div
            className={clsx(
              'flex border-b border-kyx-800/30',
              desktopExpanded ? 'items-center gap-3 px-4 py-4' : 'lg:flex-col lg:items-center lg:justify-center lg:gap-2 lg:px-2 lg:py-4',
              'max-lg:items-center max-lg:gap-3 max-lg:px-4 max-lg:py-4'
            )}
          >
            <div className="p-2 rounded-xl bg-gradient-to-br from-kyx-600/60 to-kyx-700/80 shadow-lg shadow-kyx-600/10 shrink-0">
              <FileText className="w-6 h-6 text-white" />
            </div>
            <div className={clsx('flex-1 min-w-0', !desktopExpanded && 'lg:hidden')}>
              <h1 className="text-lg font-bold text-white leading-tight">KYX - DocEngine</h1>
              <p className="text-xs text-kyx-400">Painel Administrativo</p>
            </div>
            <button
              type="button"
              onClick={() => setMobileSidebarOpen(false)}
              className="lg:hidden p-2 rounded-lg text-kyx-400 hover:text-white hover:bg-kyx-800/50 shrink-0"
              aria-label="Fechar menu"
            >
              <X size={22} />
            </button>
          </div>

          <nav className="flex-1 px-2 py-3 space-y-1 overflow-y-auto">
            {navigation.map((item) => {
              const isActive = location.pathname === item.href;
              return (
                <Link
                  key={item.name}
                  to={item.href}
                  title={item.name}
                  onClick={() => setMobileSidebarOpen(false)}
                  className={clsx(
                    'flex items-center rounded-xl transition-all duration-200',
                    desktopExpanded ? 'gap-3 px-3 py-3' : 'lg:justify-center lg:px-2 lg:py-3',
                    'max-lg:gap-3 max-lg:px-3 max-lg:py-3',
                    isActive
                      ? clsx(
                          'text-white',
                          desktopExpanded
                            ? 'bg-kyx-700/40 shadow-lg shadow-kyx-700/20'
                            : 'lg:bg-kyx-700/35 lg:shadow-none lg:border-l-2 lg:border-kyx-400 lg:rounded-l-lg lg:rounded-r-xl'
                        )
                      : 'text-kyx-400 hover:text-white hover:bg-kyx-800/50'
                  )}
                >
                  <item.icon size={20} className="shrink-0" />
                  <span className={clsx('font-medium truncate', !desktopExpanded && 'lg:hidden')}>{item.name}</span>
                </Link>
              );
            })}
          </nav>

          <div className={clsx('p-3 border-t border-kyx-800/30', !desktopExpanded && 'lg:p-2')}>
            <div
              className={clsx(
                'flex items-center gap-2 rounded-xl bg-kyx-900/50',
                desktopExpanded ? 'px-2 py-2' : 'lg:flex-col lg:px-1 lg:py-3 lg:gap-3',
                'max-lg:px-2 max-lg:py-2'
              )}
            >
              <div className="w-10 h-10 rounded-full bg-gradient-to-br from-kyx-500/60 to-kyx-600/80 flex items-center justify-center shrink-0">
                <span className="text-white font-bold text-sm">
                  {user?.nome?.charAt(0).toUpperCase() || 'U'}
                </span>
              </div>
              <div className={clsx('flex-1 min-w-0', !desktopExpanded && 'lg:hidden')}>
                <p className="text-sm font-medium text-white truncate">{user?.nome || 'Usuário'}</p>
                <p className="text-xs text-kyx-400 truncate">{user?.perfil || 'Perfil'}</p>
              </div>
              <button
                type="button"
                onClick={handleLogout}
                className="p-2 rounded-lg text-kyx-400 hover:text-white hover:bg-kyx-800/50 transition-colors shrink-0"
                title="Sair"
                aria-label="Sair"
              >
                <LogOut size={18} />
              </button>
            </div>
          </div>
        </div>

        <button
          type="button"
          onClick={() => setDesktopExpanded((e) => !e)}
          className="hidden lg:flex absolute top-1/2 right-0 z-50 h-16 w-5 translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-r-lg border border-kyx-700/50 bg-kyx-900/95 text-kyx-400 shadow-lg shadow-black/25 backdrop-blur-sm transition-colors hover:border-kyx-600 hover:bg-kyx-800 hover:text-white"
          title={desktopExpanded ? 'Recolher menu' : 'Expandir menu'}
          aria-expanded={desktopExpanded}
          aria-label={desktopExpanded ? 'Recolher menu lateral' : 'Expandir menu lateral'}
        >
          {desktopExpanded ? <ChevronLeft size={18} /> : <ChevronRight size={18} />}
        </button>
      </aside>

      <main
        className={clsx(
          'min-h-screen relative transition-[padding] duration-300 ease-out',
          desktopExpanded ? 'lg:pl-64' : 'lg:pl-20'
        )}
      >
        <div className="p-6 lg:p-8">
          <Outlet />
        </div>
      </main>

      {mobileSidebarOpen && (
        <div
          className="fixed inset-0 bg-black/50 z-30 lg:hidden"
          onClick={() => setMobileSidebarOpen(false)}
          aria-hidden
        />
      )}
    </div>
  );
}
