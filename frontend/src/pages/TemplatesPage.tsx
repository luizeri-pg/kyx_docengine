import { FormEvent, useEffect, useMemo, useState } from 'react';
import { templatesApi, type TemplateResponse, type UpsertTemplateRequest } from '../services/api';

const emptyForm: UpsertTemplateRequest = {
  slug: '',
  name: '',
  type: 'html',
  content: '',
  requiredFields: [],
};

/** Fluxo alinhado ao SDD: templates HTML (Handlebars {{var}}) ou AcroForm (PDF em base64). */
export function TemplatesPage() {
  const [items, setItems] = useState<TemplateResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [loadingEdit, setLoadingEdit] = useState(false);
  const [error, setError] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [requiredFieldsText, setRequiredFieldsText] = useState('');
  const [inspectFields, setInspectFields] = useState<string[] | null>(null);
  const [form, setForm] = useState<UpsertTemplateRequest>(emptyForm);

  const isEditing = useMemo(() => Boolean(editingId), [editingId]);

  const loadTemplates = async () => {
    setLoading(true);
    setError('');
    try {
      const response = await templatesApi.list();
      setItems(response.resultado ?? []);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { mensagem?: string } } };
      setError(e?.response?.data?.mensagem || 'Falha ao carregar templates.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadTemplates();
  }, []);

  const resetForm = () => {
    setEditingId(null);
    setRequiredFieldsText('');
    setInspectFields(null);
    setForm(emptyForm);
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError('');
    try {
      const payload: UpsertTemplateRequest = {
        ...form,
        requiredFields: requiredFieldsText
          .split(',')
          .map((f) => f.trim())
          .filter(Boolean),
      };

      if (editingId) {
        await templatesApi.update(editingId, payload);
      } else {
        await templatesApi.create(payload);
      }
      await loadTemplates();
      resetForm();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { mensagem?: string } } };
      setError(e?.response?.data?.mensagem || 'Falha ao salvar template.');
    } finally {
      setSaving(false);
    }
  };

  const editItem = async (item: TemplateResponse) => {
    setLoadingEdit(true);
    setError('');
    setInspectFields(null);
    try {
      const response = await templatesApi.getById(item.id);
      const full = response.resultado;
      if (!full) {
        setError('Template não encontrado.');
        return;
      }
      let parsed: string[] = [];
      try {
        parsed = JSON.parse(full.requiredFields || '[]');
      } catch {
        parsed = [];
      }
      setEditingId(item.id);
      setRequiredFieldsText(parsed.join(', '));
      setForm({
        slug: full.slug,
        name: full.name,
        type: full.type === 'acroform' ? 'acroform' : 'html',
        content: full.content ?? '',
        requiredFields: parsed,
      });
    } catch (err: unknown) {
      const e = err as { response?: { data?: { mensagem?: string } } };
      setError(e?.response?.data?.mensagem || 'Falha ao carregar template para edição.');
    } finally {
      setLoadingEdit(false);
    }
  };

  const removeItem = async (id: string) => {
    if (!window.confirm('Deseja desativar este template?')) return;
    try {
      await templatesApi.remove(id);
      await loadTemplates();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { mensagem?: string } } };
      setError(e?.response?.data?.mensagem || 'Falha ao remover template.');
    }
  };

  const runInspectPdf = async () => {
    if (form.type !== 'acroform' || !form.content.trim()) {
      setError('Informe o PDF em base64 no conteúdo (tipo acroform) antes de inspecionar.');
      return;
    }
    setError('');
    try {
      const res = await templatesApi.inspectPdf(form.content.trim());
      const fields = (res.resultado as { fields?: string[] } | undefined)?.fields ?? [];
      setInspectFields(fields);
      if (fields.length && !requiredFieldsText.trim()) {
        setRequiredFieldsText(fields.join(', '));
      }
    } catch (err: unknown) {
      const e = err as { response?: { data?: { mensagem?: string } } };
      setError(e?.response?.data?.mensagem || 'Falha ao inspecionar PDF.');
      setInspectFields(null);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-white">Templates</h2>
        <p className="text-kyx-400 max-w-2xl">
          Cadastro de templates para geração de PDF (SDD): tipo <strong className="text-kyx-300">html</strong> com variáveis{' '}
          <code className="text-kyx-200">{'{{nome}}'}</code> ou <strong className="text-kyx-300">acroform</strong> com PDF base64
          e campos alinhados aos nomes do formulário.
        </p>
      </div>

      {error && <div className="p-3 rounded bg-danger-500/10 border border-danger-500/30 text-danger-500">{error}</div>}

      <div className="grid lg:grid-cols-2 gap-6">
        <div className="card p-5 space-y-4">
          <h3 className="text-lg font-semibold text-white">{isEditing ? 'Editar template' : 'Novo template'}</h3>
          {loadingEdit && <p className="text-sm text-kyx-400">Carregando conteúdo…</p>}
          <form onSubmit={submit} className="space-y-3">
            <div>
              <label className="block text-xs text-kyx-500 mb-1">Slug (identificador na API)</label>
              <input className="input" placeholder="ex: fideliza_termo_genero" value={form.slug} onChange={(e) => setForm((p) => ({ ...p, slug: e.target.value }))} required />
            </div>
            <div>
              <label className="block text-xs text-kyx-500 mb-1">Nome</label>
              <input className="input" placeholder="Nome amigável" value={form.name} onChange={(e) => setForm((p) => ({ ...p, name: e.target.value }))} required />
            </div>
            <div>
              <label className="block text-xs text-kyx-500 mb-1">Tipo</label>
              <select className="input" value={form.type} onChange={(e) => setForm((p) => ({ ...p, type: e.target.value as 'html' | 'acroform' }))}>
                <option value="html">html — HTML + variáveis Handlebars</option>
                <option value="acroform">acroform — PDF base64 (AcroForm)</option>
              </select>
            </div>
            <div>
              <label className="block text-xs text-kyx-500 mb-1">Conteúdo</label>
              <textarea
                className="input min-h-[180px] font-mono text-sm"
                placeholder={form.type === 'html' ? '<!DOCTYPE html>... {{campo}} ...' : 'Cole aqui o PDF completo em base64'}
                value={form.content}
                onChange={(e) => setForm((p) => ({ ...p, content: e.target.value }))}
                required
              />
            </div>
            {form.type === 'acroform' && (
              <button type="button" className="btn text-sm" onClick={runInspectPdf}>
                Inspecionar campos do PDF (POST /templates/inspect-pdf)
              </button>
            )}
            {inspectFields && (
              <div className="p-3 rounded-lg bg-kyx-900/50 border border-kyx-800/40 text-sm">
                <p className="text-kyx-300 mb-1">Campos detectados ({inspectFields.length}):</p>
                <p className="text-kyx-400 font-mono break-all">{inspectFields.join(', ')}</p>
              </div>
            )}
            <div>
              <label className="block text-xs text-kyx-500 mb-1">Campos obrigatórios (vírgula) — chaves em Dados na geração</label>
              <input
                className="input"
                placeholder="nome, cpf, dataNascimento"
                value={requiredFieldsText}
                onChange={(e) => setRequiredFieldsText(e.target.value)}
              />
            </div>
            <div className="flex gap-2 flex-wrap">
              <button type="submit" className="btn btn-primary" disabled={saving || loadingEdit}>
                {saving ? 'Salvando...' : isEditing ? 'Atualizar' : 'Criar'}
              </button>
              {isEditing && (
                <button type="button" className="btn" onClick={resetForm}>
                  Cancelar
                </button>
              )}
            </div>
          </form>
        </div>

        <div className="card p-5">
          <h3 className="text-lg font-semibold text-white mb-3">Templates ativos</h3>
          {loading ? (
            <p className="text-kyx-400">Carregando...</p>
          ) : (
            <div className="space-y-3 max-h-[560px] overflow-auto pr-1">
              {items.map((item) => (
                <div key={item.id} className="p-3 rounded-lg border border-kyx-800/40 bg-kyx-900/30">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="text-white font-medium">{item.name}</p>
                      <p className="text-xs text-kyx-400 font-mono">{item.slug}</p>
                      <span className="inline-block mt-1 text-[10px] uppercase tracking-wide px-2 py-0.5 rounded bg-kyx-800/80 text-kyx-300">{item.type}</span>
                    </div>
                    <div className="flex gap-2 flex-shrink-0">
                      <button type="button" className="btn text-sm" onClick={() => editItem(item)} disabled={loadingEdit}>
                        Editar
                      </button>
                      <button type="button" className="btn text-sm" onClick={() => removeItem(item.id)}>
                        Desativar
                      </button>
                    </div>
                  </div>
                </div>
              ))}
              {!items.length && <p className="text-kyx-500">Nenhum template ativo.</p>}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
