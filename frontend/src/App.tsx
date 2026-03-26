import { Routes, Route, Navigate } from 'react-router-dom';
import { useAuthStore } from './stores/authStore';
import { Layout } from './components/Layout';
import { LoginPage } from './pages/LoginPage';
import { NotifyDashboardPage } from './pages/NotifyDashboardPage';
import { GenerateDocumentPage } from './pages/GenerateDocumentPage';
import { HistoricoPage } from './pages/HistoricoPage';
import { AuditoriaPage } from './pages/AuditoriaPage';
import { UsersPage } from './pages/UsersPage';
import { NotifyIntegracoesPage } from './pages/NotifyIntegracoesPage';

function PrivateRoute({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      
      <Route
        path="/"
        element={
          <PrivateRoute>
            <Layout />
          </PrivateRoute>
        }
      >
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="dashboard" element={<NotifyDashboardPage />} />
        <Route path="generate" element={<GenerateDocumentPage />} />
        <Route path="historico" element={<HistoricoPage />} />
        <Route path="auditoria" element={<AuditoriaPage />} />
        <Route path="users" element={<UsersPage />} />
        <Route path="integracoes" element={<NotifyIntegracoesPage />} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

