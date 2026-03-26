import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  Pencil,
  Plus,
  Search,
  Shield,
  Trash2,
  UserCircle2,
  Users as UsersIcon,
} from 'lucide-react';
import clsx from 'clsx';
import {
  usuariosApi,
  type CreateUsuarioRequest,
  type Perfil,
  type UpdateUsuarioRequest,
  type Usuario,
} from '../services/api';

function initials(nome: string) {
  const parts = nome.trim().split(/\s+/).filter(Boolean);
  if (!parts.length) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function formatDate(iso?: string) {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleDateString('pt-BR');
  } catch {
    return '—';
  }
}

const emptyCreate: CreateUsuarioRequest = {
  nome: '',
  email: '',
  senha: '',
  perfilId: '',
  ativo: true,
};

/** Tela administrativa de usuários alinhada ao DocEngine (sem branding NotifyHUB). */
export function UsersPage() {
  const [items, setItems] = useState<Usuario[]>([]);
  const [perfis, setPerfis] = useState<Perfil[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<CreateUsuarioRequest>(emptyCreate);

  const loadAll = async () => {
    setLoading(true);
    setError('');
    try {
      const [uRes, pRes] = await Promise.all([usuariosApi.list(), usuariosApi.perfis()]);
      setItems(uRes.resultado ?? []);
      setPerfis(pRes.resultado ?? []);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { mensagem?: string } } };
      setError(e?.response?.data?.mensagem || 'Não foi possível carregar usuários. Verifique a API e o CORS.');
      setItems([]);
      setPerfis([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadAll();
  }, []);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return items;
    return items.filter(
      (u) =>
        u.nome.toLowerCase().includes(q) ||
        u.email.toLowerCase().includes(q) ||
        (u.perfil?.nome ?? '').toLowerCase().includes(q)
    );
  }, [items, search]);

  const stats = useMemo(() => {
    const total = items.length;
    const ativos = items.filter((u) => u.ativo).length;
    const inativos = total - ativos;
    return { total, ativos, inativos };
  }, [items]);

  const openCreate = () => {
    setError('');
    setEditingId(null);
    setForm({ ...emptyCreate, perfilId: perfis[0]?.id ?? '' });
    setModalOpen(true);
  };

  const openEdit = (u: Usuario) => {
    setError('');
    setEditingId(u.id);
    setForm({
      nome: u.nome,
      email: u.email,
      senha: '',
      perfilId: u.perfilId,
      ativo: u.ativo,
    });
    setModalOpen(true);
  };

  const closeModal = () => {
    setModalOpen(false);
    setEditingId(null);
    setForm(emptyCreate);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError('');
    try {
      if (editingId) {
        const payload: UpdateUsuarioRequest = {
          nome: form.nome,
          email: form.email,
          perfilId: form.perfilId,
          ativo: form.ativo,
        };
        if (form.senha.trim()) payload.senha = form.senha.trim();
        await usuariosApi.update(editingId, payload);
      } else {
        if (!form.senha.trim()) {
          setError('Informe uma senha para o novo usuário.');
          setSaving(false);
          return;
        }
        await usuariosApi.create({
          nome: form.nome,
          email: form.email,
          senha: form.senha,
          perfilId: form.perfilId,
          ativo: form.ativo,
        });
      }
      await loadAll();
      closeModal();
    } catch (err: unknown) {
      const ex = err as { response?: { data?: { mensagem?: string } } };
      setError(ex?.response?.data?.mensagem || 'Falha ao salvar usuário.');
    } finally {
      setSaving(false);
    }
  };

  const remove = async (id: string) => {
    if (!window.confirm('Remover este usuário?')) return;
    setError('');
    try {
      await usuariosApi.remove(id);
      await loadAll();
    } catch (err: unknown) {
      const ex = err as { response?: { data?: { mensagem?: string } } };
      setError(ex?.response?.data?.mensagem || 'Falha ao remover usuário.');
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="text-2xl font-bold text-white">Gestão de Usuários</h2>
          <p className="text-kyx-400 max-w-2xl mt-1">
            Gerencie usuários com acesso ao <span className="text-kyx-300">KYX DocEngine</span> (API ou painel administrativo).
          </p>
        </div>
        <button type="button" className="btn btn-primary inline-flex items-center gap-2 shrink-0" onClick={openCreate}>
          <Plus className="w-4 h-4" />
          Novo Usuário
        </button>
      </div>

      {error && !modalOpen && (
        <div className="p-3 rounded-lg bg-danger-500/10 border border-danger-500/30 text-danger-500 text-sm">{error}</div>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="card p-4 flex items-center gap-4">
          <div className="p-3 rounded-xl bg-kyx-800/60 text-kyx-300">
            <UsersIcon className="w-6 h-6" />
          </div>
          <div>
            <p className="text-kyx-400 text-sm">Total de Usuários</p>
            <p className="text-2xl font-bold text-white">{loading ? '…' : stats.total}</p>
          </div>
        </div>
        <div className="card p-4 flex items-center gap-4">
          <div className="p-3 rounded-xl bg-success-500/15 text-success-500">
            <Shield className="w-6 h-6" />
          </div>
          <div>
            <p className="text-kyx-400 text-sm">Usuários Ativos</p>
            <p className="text-2xl font-bold text-success-500">{loading ? '…' : stats.ativos}</p>
          </div>
        </div>
        <div className="card p-4 flex items-center gap-4">
          <div className="p-3 rounded-xl bg-warning-500/15 text-warning-500">
            <UserCircle2 className="w-6 h-6" />
          </div>
          <div>
            <p className="text-kyx-400 text-sm">Usuários Inativos</p>
            <p className="text-2xl font-bold text-warning-500">{loading ? '…' : stats.inativos}</p>
          </div>
        </div>
      </div>

      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-kyx-500" />
        <input
          className="input pl-10 w-full"
          placeholder="Buscar por nome ou email..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>

      <div className="card overflow-hidden p-0">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-kyx-800/50 bg-kyx-900/40 text-kyx-400 uppercase text-xs tracking-wide">
                <th className="px-4 py-3 font-medium">Usuário</th>
                <th className="px-4 py-3 font-medium">Email</th>
                <th className="px-4 py-3 font-medium">Perfil</th>
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 font-medium">Criado em</th>
                <th className="px-4 py-3 font-medium text-right">Ações</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center text-kyx-500">
                    Carregando usuários…
                  </td>
                </tr>
              ) : filtered.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center text-kyx-500">
                    Nenhum usuário encontrado.
                  </td>
                </tr>
              ) : (
                filtered.map((u) => (
                  <tr key={u.id} className="border-b border-kyx-800/30 hover:bg-kyx-900/25">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <div className="w-9 h-9 rounded-full bg-gradient-to-br from-kyx-600/80 to-kyx-800 flex items-center justify-center text-xs font-bold text-white shrink-0">
                          {initials(u.nome)}
                        </div>
                        <span className="text-white font-medium">{u.nome}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-kyx-300 font-mono text-xs">{u.email}</td>
                    <td className="px-4 py-3 text-kyx-200">{u.perfil?.nome ?? u.perfilId}</td>
                    <td className="px-4 py-3">
                      <span
                        className={clsx(
                          'text-xs font-semibold',
                          u.ativo ? 'text-success-500' : 'text-warning-500'
                        )}
                      >
                        {u.ativo ? 'Ativo' : 'Inativo'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-kyx-400">{formatDate(u.criadoEm)}</td>
                    <td className="px-4 py-3 text-right">
                      <div className="inline-flex gap-1">
                        <button
                          type="button"
                          className="p-2 rounded-lg text-kyx-400 hover:text-white hover:bg-kyx-800/60"
                          title="Editar"
                          onClick={() => openEdit(u)}
                        >
                          <Pencil className="w-4 h-4" />
                        </button>
                        <button
                          type="button"
                          className="p-2 rounded-lg text-kyx-400 hover:text-danger-500 hover:bg-danger-500/10"
                          title="Excluir"
                          onClick={() => remove(u.id)}
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
          <div className="card w-full max-w-md p-6 space-y-4 max-h-[90vh] overflow-y-auto">
            <h3 className="text-lg font-semibold text-white">{editingId ? 'Editar usuário' : 'Novo usuário'}</h3>
            {error && <div className="p-2 rounded text-sm bg-danger-500/10 border border-danger-500/30 text-danger-500">{error}</div>}
            <form onSubmit={submit} className="space-y-3">
              <div>
                <label className="block text-xs text-kyx-500 mb-1">Nome</label>
                <input
                  className="input"
                  value={form.nome}
                  onChange={(e) => setForm((p) => ({ ...p, nome: e.target.value }))}
                  required
                />
              </div>
              <div>
                <label className="block text-xs text-kyx-500 mb-1">Email / login</label>
                <input
                  className="input"
                  type="text"
                  value={form.email}
                  onChange={(e) => setForm((p) => ({ ...p, email: e.target.value }))}
                  required
                />
              </div>
              <div>
                <label className="block text-xs text-kyx-500 mb-1">
                  Senha {editingId && <span className="text-kyx-600">(deixe em branco para manter)</span>}
                </label>
                <input
                  className="input"
                  type="password"
                  autoComplete="new-password"
                  value={form.senha}
                  onChange={(e) => setForm((p) => ({ ...p, senha: e.target.value }))}
                  required={!editingId}
                />
              </div>
              <div>
                <label className="block text-xs text-kyx-500 mb-1">Perfil</label>
                <select
                  className="input"
                  value={form.perfilId}
                  onChange={(e) => setForm((p) => ({ ...p, perfilId: e.target.value }))}
                  required
                >
                  <option value="">Selecione…</option>
                  {perfis.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.nome}
                    </option>
                  ))}
                </select>
              </div>
              <label className="flex items-center gap-2 text-sm text-kyx-300 cursor-pointer">
                <input
                  type="checkbox"
                  checked={form.ativo}
                  onChange={(e) => setForm((p) => ({ ...p, ativo: e.target.checked }))}
                  className="rounded border-kyx-600"
                />
                Usuário ativo
              </label>
              <div className="flex gap-2 pt-2">
                <button type="submit" className="btn btn-primary flex-1" disabled={saving}>
                  {saving ? 'Salvando…' : editingId ? 'Salvar' : 'Criar'}
                </button>
                <button type="button" className="btn" onClick={closeModal} disabled={saving}>
                  Cancelar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
