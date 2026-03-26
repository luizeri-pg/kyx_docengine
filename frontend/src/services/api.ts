import axios from 'axios';
import { useAuthStore } from '../stores/authStore';

/** Base URL do DocEngine (backend). Sobrescreva com `VITE_API_URL` no `.env` se a API não estiver em localhost:3000. */
export const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:3000';

export const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 120000, // geração PDF / fila pode demorar
  headers: {
    'Content-Type': 'application/json',
  },
});

// Interceptor para adicionar token
api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Interceptor para tratamento de erros
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      useAuthStore.getState().logout();
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export interface ApiResponse<T> {
  sucesso: boolean;
  mensagem?: string;
  tempoProcessamento: number;
  requisicaoId: string;
  resultado?: T;
}

export interface LoginResponse {
  expires_in: number;
  access_token: string;
  token_type: string;
}

export interface TemplateResponse {
  id: string;
  slug: string;
  name: string;
  type: string;
  /** Presente em GET por id e após criar/atualizar */
  content?: string | null;
  requiredFields: string;
  isActive: boolean;
}

export interface UpsertTemplateRequest {
  slug: string;
  name: string;
  type: 'html' | 'acroform';
  content: string;
  requiredFields: string[];
}

/** Template no corpo do pedido — não grava na tabela `templates` (use `template` **ou** `inlineTemplate`). */
export interface InlineTemplatePayload {
  type: 'html' | 'acroform';
  content: string;
  requiredFields?: string[];
}

export interface GenerateDocumentRequest {
  requisicaoId: string;
  config: {
    /** Slug do template registado; omitir se enviar `inlineTemplate`. */
    template?: string;
    centroCusto: string;
    nomeArquivo?: string;
    inlineTemplate?: InlineTemplatePayload;
  };
  dados: Record<string, string>;
}

export interface GenerateDocumentResponse {
  jobId: string;
  status: string;
}

/** PDF síncrono — sem job na BD (requer Documents:AllowSyncPdfGeneration no backend). */
export interface GenerateSyncPdfRequest {
  requisicaoId?: string;
  nomeArquivo?: string;
  inlineTemplate: InlineTemplatePayload;
  dados: Record<string, string>;
}

export interface DocumentResult {
  base64: string;
  contentType: string;
  nomeArquivo: string;
}

export interface DocumentStatusPayload {
  jobId: string;
  status: string;
  errorMessage?: string;
  resultado?: DocumentResult;
}

/** Item do histórico de jobs (GET /documents/jobs) */
export interface DocumentJobListItem {
  jobId: string;
  requisicaoId: string;
  templateSlug: string;
  templateName: string;
  templateType: string;
  centroCusto: string;
  nomeArquivo: string;
  status: string;
  errorMessage?: string;
  processingTimeMs?: number;
  createdAt: string;
}

/** Entrada de log (GET /audit/logs/:requisicaoId) — tabela tb_log_requisicao */
export interface RequestLogEntry {
  id: string;
  requisicaoId: string;
  endpoint: string;
  requestBody: string;
  responseBody?: string;
  httpStatusCode?: number;
  userId?: string;
  centroCusto?: string;
  durationMs?: number;
  createdAt: string;
  /** ex.: docengine, email */
  canal?: string;
  erro?: string;
}

export interface Perfil {
  id: string;
  nome: string;
  descricao?: string;
}

export interface Usuario {
  id: string;
  nome: string;
  email: string;
  perfilId: string;
  ativo: boolean;
  perfil?: Perfil | null;
  /** ISO — retornado pelo backend (UsuarioDto) */
  criadoEm?: string;
  atualizadoEm?: string;
}

export interface CreateUsuarioRequest {
  nome: string;
  email: string;
  senha: string;
  perfilId: string;
  ativo: boolean;
}

export interface UpdateUsuarioRequest {
  nome?: string;
  email?: string;
  senha?: string;
  perfilId?: string;
  ativo?: boolean;
}

export interface LogRequisicao {
  id: string;
  requisicaoId: string;
  usuarioId?: string | null;
  canal?: string | null;
  centroCusto?: string | null;
  statusHttp?: number | null;
  tempoRespostaMs?: number | null;
  erro?: string | null;
  criadoEm: string;
}

export interface LogIntegracao {
  id: string;
  requisicaoId: string;
  integracaoId: string;
  endpoint?: string | null;
  metodo?: string | null;
  statusHttp?: number | null;
  tempoRespostaMs?: number | null;
  criadoEm: string;
}

export interface LogDetalhe {
  requisicao: LogRequisicao;
  integracoes: LogIntegracao[];
}

export interface NotificationSendRequest {
  requisicaoId?: string;
  config: {
    canal: 'email' | 'sms' | 'whatsapp';
    centroCusto?: string;
  };
  dados: Record<string, string>;
}

export interface NotificationSendResponse {
  requisicaoId: string;
  status: string;
  mensagem?: string;
}

export interface Integracao {
  id: string;
  nome: string;
  descricao?: string;
  tipo?: string;
  canal: string;
  provedor?: string;
  urlBase?: string;
  ativo: boolean;
}

export interface DashboardMetrics {
  total: number;
  sucesso: number;
  erros: number;
  porCanal?: Record<string, number>;
}

export interface DashboardError {
  requisicaoId: string;
  canal?: string | null;
  erro: string;
  criadoEm: string;
}

// ============ Auth API ============
export const authApi = {
  login: async (username: string, password: string) => {
    const response = await api.post<ApiResponse<LoginResponse>>('/auth/login', { username, password });
    return response.data;
  },
};

// ============ Templates API ============
export const templatesApi = {
  list: async () => {
    const response = await api.get<ApiResponse<TemplateResponse[]>>('/templates');
    return response.data;
  },
  getById: async (id: string) => {
    const response = await api.get<ApiResponse<TemplateResponse>>(`/templates/${id}`);
    return response.data;
  },
  create: async (payload: UpsertTemplateRequest) => {
    const response = await api.post<ApiResponse<TemplateResponse>>('/templates', payload);
    return response.data;
  },
  update: async (id: string, payload: UpsertTemplateRequest) => {
    const response = await api.put<ApiResponse<TemplateResponse>>(`/templates/${id}`, payload);
    return response.data;
  },
  remove: async (id: string) => {
    const response = await api.delete<ApiResponse<object>>(`/templates/${id}`);
    return response.data;
  },
  inspectPdf: async (pdfBase64: string) => {
    const response = await api.post<ApiResponse<{ fields: string[] }>>('/templates/inspect-pdf', { pdfBase64 });
    return response.data;
  },
};

// ============ Documents API ============
export const documentsApi = {
  generate: async (payload: GenerateDocumentRequest) => {
    const response = await api.post<ApiResponse<GenerateDocumentResponse>>('/documents/generate', payload);
    return response.data;
  },
  /** Base64 do PDF na mesma resposta — sem document_jobs (só dev / flag no servidor). */
  generateSync: async (payload: GenerateSyncPdfRequest) => {
    const response = await api.post<ApiResponse<DocumentResult>>('/documents/generate-sync', payload);
    return response.data;
  },
  getStatus: async (jobId: string) => {
    const response = await api.get<ApiResponse<DocumentStatusPayload>>(`/documents/status/${jobId}`);
    return response.data;
  },
  listJobs: async (params?: {
    limit?: number;
    status?: string;
    templateType?: string;
    search?: string;
  }) => {
    const response = await api.get<ApiResponse<DocumentJobListItem[]>>('/documents/jobs', { params });
    return response.data;
  },
};

export const auditApi = {
  logsByRequisicao: async (requisicaoId: string) => {
    const response = await api.get<ApiResponse<RequestLogEntry[]>>(
      `/audit/logs/${encodeURIComponent(requisicaoId)}`
    );
    return response.data;
  },
};

// ============ Usuários API ============
export const usuariosApi = {
  list: async () => {
    const response = await api.get<ApiResponse<Usuario[]>>('/usuarios');
    return response.data;
  },
  perfis: async () => {
    const response = await api.get<ApiResponse<Perfil[]>>('/usuarios/perfis/list');
    return response.data;
  },
  create: async (payload: CreateUsuarioRequest) => {
    const response = await api.post<ApiResponse<Usuario>>('/usuarios', payload);
    return response.data;
  },
  update: async (id: string, payload: UpdateUsuarioRequest) => {
    const response = await api.put<ApiResponse<Usuario>>(`/usuarios/${id}`, payload);
    return response.data;
  },
  remove: async (id: string) => {
    const response = await api.delete<ApiResponse<object>>(`/usuarios/${id}`);
    return response.data;
  },
};

// ============ Logs/Auditoria API ============
export const logsApi = {
  history: async (params?: { canal?: string; centroCusto?: string; limit?: number; offset?: number }) => {
    const response = await api.get<ApiResponse<LogRequisicao[]>>('/dashboard/history', { params });
    return response.data;
  },
  detalhes: async (requisicaoId: string) => {
    const response = await api.get<ApiResponse<LogDetalhe>>(`/dashboard/logs/${encodeURIComponent(requisicaoId)}`);
    return response.data;
  },
};

export const dashboardApi = {
  metrics: async () => {
    const response = await api.get<ApiResponse<DashboardMetrics>>('/dashboard/metrics');
    return response.data;
  },
  errors: async () => {
    const response = await api.get<ApiResponse<DashboardError[]>>('/dashboard/errors');
    return response.data;
  },
  history: async (params?: { canal?: string; centroCusto?: string; limit?: number; offset?: number }) => {
    const response = await api.get<ApiResponse<LogRequisicao[]>>('/dashboard/history', { params });
    return response.data;
  },
  detalhes: async (requisicaoId: string) => {
    const response = await api.get<ApiResponse<LogDetalhe>>(`/dashboard/logs/${encodeURIComponent(requisicaoId)}`);
    return response.data;
  },
};

export const notificationApi = {
  send: async (payload: NotificationSendRequest) => {
    const response = await api.post<ApiResponse<NotificationSendResponse>>('/notification/send', payload);
    return response.data;
  },
};

export const integracoesApi = {
  list: async (params?: { canal?: string; ativo?: boolean }) => {
    const response = await api.get<ApiResponse<Integracao[]>>('/integracoes', { params });
    return response.data;
  },
};
