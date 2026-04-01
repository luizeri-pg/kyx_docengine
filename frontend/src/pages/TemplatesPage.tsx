import { FormEvent, useEffect, useMemo, useState } from 'react';
import { templatesApi, type TemplateResponse, type UpsertTemplateRequest } from '../services/api';

const emptyForm: UpsertTemplateRequest = {
  slug: '',
  name: '',
  type: 'html',
  content: '',
  requiredFields: [],
};
const LOCAL_TEMPLATES_KEY = 'kyx_docengine_mock_templates_v1';

type FieldType = 'text' | 'email' | 'date' | 'phone' | 'checkbox';
type BuilderField = {
  id: string;
  section: string;
  key: string;
  label: string;
  type: FieldType;
  width: 'full' | 'half';
};
type BlockType = 'title' | 'text' | 'fields' | 'table' | 'twoCols' | 'signature' | 'divider';
type BuilderBlock =
  | {
      id: string;
      type: 'title';
      badge: string;
      title: string;
      subtitle: string;
    }
  | {
      id: string;
      type: 'text';
      title: string;
      text: string;
    }
  | {
      id: string;
      type: 'fields';
      title: string;
      layout: 'table' | 'cards';
      fields: BuilderField[];
    }
  | {
      id: string;
      type: 'table';
      title: string;
      columns: string[];
      rows: string[][];
    }
  | {
      id: string;
      type: 'twoCols';
      leftTitle: string;
      leftText: string;
      rightTitle: string;
      rightText: string;
    }
  | {
      id: string;
      type: 'signature';
      label: string;
      nameKey: string;
      dateKey: string;
    }
  | {
      id: string;
      type: 'divider';
    };

/** Paleta usada no CSS gerado (modos estruturado e blocos). */
type PdfTheme = {
  primary: string;
  soft: string;
  pageBg: string;
  sheetBg: string;
  text: string;
  muted: string;
  border: string;
  tableStripe: string;
};

const DEFAULT_PDF_THEME: PdfTheme = {
  primary: '#0b5c7a',
  soft: '#e3f2f7',
  pageBg: '#eef2f5',
  sheetBg: '#ffffff',
  text: '#1a1a1a',
  muted: '#56616a',
  border: '#e8ecef',
  tableStripe: '#fbfdff',
};

function pdfThemeRootStyle(theme: PdfTheme): string {
  return `:root {
    --pdf-primary: ${theme.primary};
    --pdf-soft: ${theme.soft};
    --pdf-page-bg: ${theme.pageBg};
    --pdf-sheet-bg: ${theme.sheetBg};
    --pdf-text: ${theme.text};
    --pdf-muted: ${theme.muted};
    --pdf-border: ${theme.border};
    --pdf-table-stripe: ${theme.tableStripe};
  }`;
}

const PDF_THEME_CONTROLS: { key: keyof PdfTheme; label: string }[] = [
  { key: 'primary', label: 'Primária (títulos, bordas)' },
  { key: 'soft', label: 'Destaque suave (badge, cabeçalho de tabela)' },
  { key: 'pageBg', label: 'Fundo da página' },
  { key: 'sheetBg', label: 'Fundo da folha' },
  { key: 'text', label: 'Texto principal' },
  { key: 'muted', label: 'Texto secundário' },
  { key: 'border', label: 'Linhas e bordas' },
  { key: 'tableStripe', label: 'Fundo zebrado (cards / tabela)' },
];

const createDefaultBuilderFields = (): BuilderField[] => [
  { id: crypto.randomUUID(), section: 'Dados pessoais', key: 'nomeCompleto', label: 'Nome completo', type: 'text', width: 'full' },
  { id: crypto.randomUUID(), section: 'Dados pessoais', key: 'cpf', label: 'CPF', type: 'text', width: 'half' },
  { id: crypto.randomUUID(), section: 'Dados pessoais', key: 'dataNascimento', label: 'Data de nascimento', type: 'date', width: 'half' },
  { id: crypto.randomUUID(), section: 'Contato', key: 'email', label: 'E-mail', type: 'email', width: 'full' },
  { id: crypto.randomUUID(), section: 'Contato', key: 'telefone', label: 'Telefone', type: 'phone', width: 'half' },
];
const createInitialBlocks = (): BuilderBlock[] => [
  {
    id: crypto.randomUUID(),
    type: 'title',
    badge: 'Modelo visual',
    title: 'Formulário dinâmico',
    subtitle: 'Documento criado no construtor de blocos',
  },
  {
    id: crypto.randomUUID(),
    type: 'fields',
    title: 'Dados',
    layout: 'cards',
    fields: createDefaultBuilderFields(),
  },
  {
    id: crypto.randomUUID(),
    type: 'table',
    title: 'Tabela de itens',
    columns: ['Descrição', 'Valor'],
    rows: [
      ['{{itemDescricao1}}', '{{itemValor1}}'],
      ['{{itemDescricao2}}', '{{itemValor2}}'],
    ],
  },
  {
    id: crypto.randomUUID(),
    type: 'signature',
    label: 'Assinatura do responsável',
    nameKey: 'assinaturaNome',
    dateKey: 'dataAssinatura',
  },
];

function buildTemplateHtml(
  title: string,
  subtitle: string,
  badge: string,
  fields: BuilderField[],
  layoutStyle: 'table' | 'cards',
  theme: PdfTheme
) {
  const required = Array.from(new Set(fields.map((f) => f.key.trim()).filter(Boolean)));
  const groups = fields.reduce<Record<string, BuilderField[]>>((acc, field) => {
    const section = field.section.trim() || 'Dados gerais';
    if (!acc[section]) acc[section] = [];
    acc[section].push(field);
    return acc;
  }, {});

  const sectionHtml = Object.entries(groups)
    .map(([section, sectionFields]) => {
      const cleanFields = sectionFields.filter((f) => f.key.trim() && f.label.trim());

      if (layoutStyle === 'cards') {
        const cards = cleanFields
          .map((f) => {
            const value =
              f.type === 'checkbox'
                ? `<span class="check">{{${f.key}}}</span>`
                : `{{${f.key}}}`;
            return `<div class="item ${f.width === 'half' ? 'half' : 'full'}"><div class="k">${f.label}</div><div class="v">${value}</div></div>`;
          })
          .join('\n');
        return `
  <h2>${section}</h2>
  <div class="grid">
    ${cards}
  </div>`;
      }

      const rows = cleanFields
        .map((f) =>
          f.type === 'checkbox'
            ? `<tr><td class="k">${f.label}</td><td class="v"><span class="check">{{${f.key}}}</span></td></tr>`
            : `<tr><td class="k">${f.label}</td><td class="v">{{${f.key}}}</td></tr>`
        )
        .join('\n');
      return `
  <h2>${section}</h2>
  <table class="data">
    ${rows}
  </table>`;
    })
    .join('\n');

  const html = `<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="utf-8"/>
<style>
${pdfThemeRootStyle(theme)}
  * { box-sizing: border-box; }
  body { font-family: 'Segoe UI', system-ui, sans-serif; margin: 0; padding: 40px; background: var(--pdf-page-bg); color: var(--pdf-text); }
  .sheet { background: var(--pdf-sheet-bg); max-width: 720px; margin: 0 auto; padding: 36px 40px; border-radius: 8px; box-shadow: 0 2px 12px rgba(0,0,0,.08); }
  h1 { font-size: 22px; color: var(--pdf-primary); margin: 0 0 8px; border-bottom: 2px solid var(--pdf-primary); padding-bottom: 12px; }
  .sub { color: var(--pdf-muted); font-size: 13px; margin-bottom: 24px; }
  h2 { font-size: 14px; text-transform: uppercase; letter-spacing: .06em; color: var(--pdf-primary); margin: 22px 0 10px; }
  table.data { width: 100%; border-collapse: collapse; font-size: 13px; margin-bottom: 14px; }
  table.data td { padding: 8px 10px; border-bottom: 1px solid var(--pdf-border); vertical-align: top; }
  table.data td.k { width: 38%; color: var(--pdf-muted); font-weight: 600; }
  table.data td.v { color: var(--pdf-text); }
  .grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; margin-bottom: 14px; }
  .item { border: 1px solid var(--pdf-border); border-radius: 6px; background: var(--pdf-table-stripe); padding: 10px; }
  .item.half { grid-column: span 1; }
  .item.full { grid-column: 1 / -1; }
  .item .k { color: var(--pdf-muted); font-weight: 600; font-size: 12px; margin-bottom: 5px; }
  .item .v { color: var(--pdf-text); font-size: 13px; }
  .badge { display: inline-block; background: var(--pdf-soft); color: var(--pdf-primary); padding: 4px 10px; border-radius: 4px; font-size: 12px; margin-bottom: 16px; }
  .check { display:inline-block; width:18px; text-align:center; border:1px solid var(--pdf-primary); border-radius:4px; margin-right:6px; }
</style>
</head>
<body>
<div class="sheet">
  <span class="badge">${badge || 'Documento gerado pelo construtor'}</span>
  <h1>${title || 'Formulário'}</h1>
  <p class="sub">${subtitle || 'Preenchimento automático via placeholders {{campo}}'}</p>
  ${sectionHtml}
</div>
</body>
</html>`;

  return { html, required };
}

function sampleValueByType(field: BuilderField) {
  switch (field.type) {
    case 'email':
      return 'usuario@email.com';
    case 'date':
      return '26/03/2026';
    case 'phone':
      return '(11) 99999-9999';
    case 'checkbox':
      return 'X';
    default:
      return `Exemplo: ${field.label || field.key}`;
  }
}

function extractPlaceholders(html: string) {
  const matches = html.match(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g) ?? [];
  const keys = matches
    .map((m) => m.replace('{{', '').replace('}}', '').trim())
    .filter(Boolean);
  return Array.from(new Set(keys));
}

function renderFieldsSection(section: string, sectionFields: BuilderField[], layoutStyle: 'table' | 'cards') {
  const cleanFields = sectionFields.filter((f) => f.key.trim() && f.label.trim());

  if (layoutStyle === 'cards') {
    const cards = cleanFields
      .map((f) => {
        const value =
          f.type === 'checkbox'
            ? `<span class="check">{{${f.key}}}</span>`
            : `{{${f.key}}}`;
        return `<div class="item ${f.width === 'half' ? 'half' : 'full'}"><div class="k">${f.label}</div><div class="v">${value}</div></div>`;
      })
      .join('\n');
    return `
  <h2>${section}</h2>
  <div class="grid">
    ${cards}
  </div>`;
  }

  const rows = cleanFields
    .map((f) =>
      f.type === 'checkbox'
        ? `<tr><td class="k">${f.label}</td><td class="v"><span class="check">{{${f.key}}}</span></td></tr>`
        : `<tr><td class="k">${f.label}</td><td class="v">{{${f.key}}}</td></tr>`
    )
    .join('\n');
  return `
  <h2>${section}</h2>
  <table class="data">
    ${rows}
  </table>`;
}

function buildTemplateHtmlFromBlocks(blocks: BuilderBlock[], theme: PdfTheme) {
  const bodyBlocks = blocks
    .map((block) => {
      if (block.type === 'title') {
        return `
    <span class="badge">${block.badge || 'Documento'}</span>
    <h1>${block.title || 'Formulário'}</h1>
    <p class="sub">${block.subtitle || ''}</p>`;
      }
      if (block.type === 'text') {
        return `
    <h2>${block.title || 'Texto'}</h2>
    <p>${block.text || ''}</p>`;
      }
      if (block.type === 'fields') {
        return renderFieldsSection(block.title || 'Dados', block.fields, block.layout);
      }
      if (block.type === 'table') {
        const header = block.columns.map((col) => `<th>${col || 'Coluna'}</th>`).join('');
        const rows = block.rows
          .map(
            (row) =>
              `<tr>${row
                .map((cell) => `<td>${cell || ''}</td>`)
                .join('')}</tr>`
          )
          .join('');
        return `
    <h2>${block.title || 'Tabela'}</h2>
    <table class="tbl">
      <thead><tr>${header}</tr></thead>
      <tbody>${rows}</tbody>
    </table>`;
      }
      if (block.type === 'twoCols') {
        return `
    <div class="row">
      <div class="col"><div class="label">${block.leftTitle || 'Coluna esquerda'}</div>${block.leftText || ''}</div>
      <div class="col"><div class="label">${block.rightTitle || 'Coluna direita'}</div>${block.rightText || ''}</div>
    </div>`;
      }
      if (block.type === 'signature') {
        return `
    <div class="sig-box">
      <div class="sig-line"></div>
      <p class="sig-label">${block.label || 'Assinatura'}</p>
      <p class="sig-meta">{{${block.nameKey || 'assinaturaNome'}}} · {{${block.dateKey || 'dataAssinatura'}}}</p>
    </div>`;
      }
      return '<hr class="sep" />';
    })
    .join('\n');

  const html = `<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="utf-8"/>
<style>
${pdfThemeRootStyle(theme)}
  * { box-sizing: border-box; }
  body { font-family: 'Segoe UI', system-ui, sans-serif; margin: 0; padding: 40px; background: var(--pdf-page-bg); color: var(--pdf-text); }
  .sheet { background: var(--pdf-sheet-bg); max-width: 900px; margin: 0 auto; padding: 28px 30px; border-radius: 8px; box-shadow: 0 2px 12px rgba(0,0,0,.08); }
  h1 { font-size: 22px; color: var(--pdf-primary); margin: 0 0 8px; border-bottom: 2px solid var(--pdf-primary); padding-bottom: 12px; }
  .sub { color: var(--pdf-muted); font-size: 13px; margin: 0 0 24px; }
  h2 { font-size: 14px; text-transform: uppercase; letter-spacing: .06em; color: var(--pdf-primary); margin: 22px 0 10px; }
  p { line-height: 1.5; }
  table.data { width: 100%; border-collapse: collapse; font-size: 13px; margin-bottom: 14px; }
  table.data td { padding: 8px 10px; border-bottom: 1px solid var(--pdf-border); vertical-align: top; }
  table.data td.k { width: 38%; color: var(--pdf-muted); font-weight: 600; }
  table.data td.v { color: var(--pdf-text); }
  .grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; margin-bottom: 14px; }
  table.tbl { width: 100%; border-collapse: collapse; margin-bottom: 14px; font-size: 12px; }
  table.tbl th, table.tbl td { border: 1px solid var(--pdf-border); padding: 8px 10px; text-align: left; vertical-align: top; }
  table.tbl th { background: var(--pdf-soft); color: var(--pdf-primary); font-weight: 700; }
  table.tbl tbody tr:nth-child(even) td { background: var(--pdf-table-stripe); }
  .item { border: 1px solid var(--pdf-border); border-radius: 6px; background: var(--pdf-table-stripe); padding: 10px; }
  .item.half { grid-column: span 1; }
  .item.full { grid-column: 1 / -1; }
  .item .k { color: var(--pdf-muted); font-weight: 600; font-size: 12px; margin-bottom: 5px; }
  .item .v { color: var(--pdf-text); font-size: 13px; }
  .badge { display: inline-block; background: var(--pdf-soft); color: var(--pdf-primary); padding: 4px 10px; border-radius: 4px; font-size: 12px; margin-bottom: 16px; }
  .check { display:inline-block; width:18px; text-align:center; border:1px solid var(--pdf-primary); border-radius:4px; margin-right:6px; }
  .row { display: flex; gap: 12px; margin: 16px 0; }
  .col { flex: 1; border: 1px solid var(--pdf-border); border-radius: 6px; padding: 10px; background: color-mix(in srgb, var(--pdf-soft) 35%, var(--pdf-sheet-bg)); }
  .label { font-size: 12px; color: var(--pdf-muted); font-weight: 600; margin-bottom: 4px; }
  .sep { border: 0; border-top: 1px solid var(--pdf-border); margin: 18px 0; }
  .sig-box { margin-top: 18px; }
  .sig-line { height: 1px; background: var(--pdf-text); opacity: 0.85; width: 320px; max-width: 100%; margin-bottom: 8px; }
  .sig-label { margin: 0; font-size: 12px; color: var(--pdf-muted); }
  .sig-meta { margin: 2px 0 0; font-size: 11px; color: var(--pdf-muted); }
</style>
</head>
<body>
  <div class="sheet">
${bodyBlocks}
  </div>
</body>
</html>`;

  return { html, required: extractPlaceholders(html) };
}

/** Fluxo alinhado ao SDD: templates HTML (Handlebars {{var}}) ou AcroForm (PDF em base64). */
export function TemplatesPage() {
  const [items, setItems] = useState<TemplateResponse[]>([]);
  const [mockMode, setMockMode] = useState(false);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [loadingEdit, setLoadingEdit] = useState(false);
  const [error, setError] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [requiredFieldsText, setRequiredFieldsText] = useState('');
  const [inspectFields, setInspectFields] = useState<string[] | null>(null);
  const [form, setForm] = useState<UpsertTemplateRequest>(emptyForm);
  const [builderTitle, setBuilderTitle] = useState('Formulário dinâmico');
  const [builderSubtitle, setBuilderSubtitle] = useState('Documento criado no construtor de modelos');
  const [builderBadge, setBuilderBadge] = useState('Modelo visual');
  const [builderFields, setBuilderFields] = useState<BuilderField[]>(createDefaultBuilderFields());
  const [layoutStyle, setLayoutStyle] = useState<'table' | 'cards'>('cards');
  const [builderMode, setBuilderMode] = useState<'structured' | 'blocks'>('blocks');
  const [blocks, setBlocks] = useState<BuilderBlock[]>(createInitialBlocks());
  const [draggingBlockId, setDraggingBlockId] = useState<string | null>(null);
  const [previewMode, setPreviewMode] = useState<'placeholder' | 'sample'>('sample');
  const [pdfTheme, setPdfTheme] = useState<PdfTheme>(() => ({ ...DEFAULT_PDF_THEME }));

  const isEditing = useMemo(() => Boolean(editingId), [editingId]);
  const generatedFromBuilder = useMemo(() => {
    const clean = builderFields.filter((f) => f.key.trim() && f.label.trim());
    return buildTemplateHtml(builderTitle, builderSubtitle, builderBadge, clean, layoutStyle, pdfTheme);
  }, [builderBadge, builderFields, builderSubtitle, builderTitle, layoutStyle, pdfTheme]);
  const generatedFromBlocks = useMemo(() => buildTemplateHtmlFromBlocks(blocks, pdfTheme), [blocks, pdfTheme]);
  const previewHtml = useMemo(() => {
    if (builderMode === 'blocks') {
      if (previewMode === 'placeholder') return generatedFromBlocks.html;
      return generatedFromBlocks.required.reduce((acc, key) => {
        const token = new RegExp(`\\{\\{\\s*${key}\\s*\\}\\}`, 'g');
        return acc.replace(token, `Exemplo: ${key}`);
      }, generatedFromBlocks.html);
    }
    if (previewMode === 'placeholder') return generatedFromBuilder.html;
    return builderFields
      .filter((f) => f.key.trim())
      .reduce((acc, field) => {
        const token = new RegExp(`\\{\\{\\s*${field.key.trim()}\\s*\\}\\}`, 'g');
        return acc.replace(token, sampleValueByType(field));
      }, generatedFromBuilder.html);
  }, [
    builderFields,
    builderMode,
    generatedFromBlocks.html,
    generatedFromBlocks.required,
    generatedFromBuilder.html,
    previewMode,
  ]);

  const loadLocalTemplates = () => {
    try {
      const raw = localStorage.getItem(LOCAL_TEMPLATES_KEY);
      if (!raw) return [];
      const parsed = JSON.parse(raw) as TemplateResponse[];
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  };

  const saveLocalTemplates = (templates: TemplateResponse[]) => {
    localStorage.setItem(LOCAL_TEMPLATES_KEY, JSON.stringify(templates));
    setItems(templates);
  };

  const loadTemplates = async () => {
    setLoading(true);
    setError('');
    try {
      if (mockMode) {
        setItems(loadLocalTemplates());
      } else {
        const response = await templatesApi.list();
        setItems(response.resultado ?? []);
      }
    } catch (err: unknown) {
      // Se API falhar (ex.: BD), entra em modo local para não bloquear o construtor.
      setMockMode(true);
      setItems(loadLocalTemplates());
      setError('API indisponível no momento. Modo mock/local ativado para continuar construindo os formulários.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadTemplates();
  }, [mockMode]);

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

      if (mockMode) {
        const nextItem: TemplateResponse = {
          id: editingId || crypto.randomUUID(),
          slug: payload.slug,
          name: payload.name,
          type: payload.type,
          content: payload.content,
          requiredFields: JSON.stringify(payload.requiredFields),
          isActive: true,
        };
        const current = loadLocalTemplates();
        const updated = editingId
          ? current.map((it) => (it.id === editingId ? nextItem : it))
          : [nextItem, ...current];
        saveLocalTemplates(updated);
      } else {
        if (editingId) {
          await templatesApi.update(editingId, payload);
        } else {
          await templatesApi.create(payload);
        }
        await loadTemplates();
      }
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
      const full = mockMode ? item : (await templatesApi.getById(item.id)).resultado;
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
      if (mockMode) {
        const updated = loadLocalTemplates().filter((it) => it.id !== id);
        saveLocalTemplates(updated);
      } else {
        await templatesApi.remove(id);
        await loadTemplates();
      }
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

  const addBuilderField = () => {
    setBuilderFields((prev) => [
      ...prev,
      {
        id: crypto.randomUUID(),
        section: 'Dados gerais',
        key: '',
        label: '',
        type: 'text',
        width: 'half',
      },
    ]);
  };

  const removeBuilderField = (id: string) => {
    setBuilderFields((prev) => prev.filter((f) => f.id !== id));
  };

  const updateBuilderField = (id: string, patch: Partial<BuilderField>) => {
    setBuilderFields((prev) => prev.map((f) => (f.id === id ? { ...f, ...patch } : f)));
  };

  const applyBuilderToTemplate = () => {
    if (builderMode === 'blocks') {
      setForm((prev) => ({ ...prev, type: 'html', content: generatedFromBlocks.html }));
      setRequiredFieldsText(generatedFromBlocks.required.join(', '));
      setError('');
      return;
    }
    const clean = builderFields.filter((f) => f.key.trim() && f.label.trim());
    if (!clean.length) {
      setError('Adicione ao menos 1 campo válido no construtor.');
      return;
    }
    setForm((prev) => ({ ...prev, type: 'html', content: generatedFromBuilder.html }));
    setRequiredFieldsText(generatedFromBuilder.required.join(', '));
    setError('');
  };

  const addBlock = (type: BlockType) => {
    const newBlock: BuilderBlock =
      type === 'title'
        ? { id: crypto.randomUUID(), type: 'title', badge: 'Novo bloco', title: 'Título', subtitle: 'Subtítulo' }
        : type === 'text'
          ? { id: crypto.randomUUID(), type: 'text', title: 'Texto livre', text: 'Edite este conteúdo.' }
          : type === 'fields'
            ? { id: crypto.randomUUID(), type: 'fields', title: 'Campos', layout: 'cards', fields: createDefaultBuilderFields().slice(0, 2) }
            : type === 'table'
              ? {
                  id: crypto.randomUUID(),
                  type: 'table',
                  title: 'Tabela',
                  columns: ['Coluna 1', 'Coluna 2'],
                  rows: [
                    ['{{valor1}}', '{{valor2}}'],
                    ['{{valor3}}', '{{valor4}}'],
                  ],
                }
            : type === 'twoCols'
              ? {
                  id: crypto.randomUUID(),
                  type: 'twoCols',
                  leftTitle: 'Coluna esquerda',
                  leftText: 'Texto da esquerda',
                  rightTitle: 'Coluna direita',
                  rightText: 'Texto da direita',
                }
              : type === 'signature'
                ? { id: crypto.randomUUID(), type: 'signature', label: 'Assinatura', nameKey: 'assinaturaNome', dateKey: 'dataAssinatura' }
                : { id: crypto.randomUUID(), type: 'divider' };
    setBlocks((prev) => [...prev, newBlock]);
  };

  const removeBlock = (id: string) => {
    setBlocks((prev) => prev.filter((b) => b.id !== id));
  };

  const updateBlock = (id: string, patch: Partial<BuilderBlock>) => {
    setBlocks((prev) => prev.map((b) => (b.id === id ? ({ ...b, ...patch } as BuilderBlock) : b)));
  };

  const moveBlock = (fromId: string, toId: string) => {
    if (fromId === toId) return;
    setBlocks((prev) => {
      const fromIndex = prev.findIndex((b) => b.id === fromId);
      const toIndex = prev.findIndex((b) => b.id === toId);
      if (fromIndex < 0 || toIndex < 0) return prev;
      const next = [...prev];
      const [item] = next.splice(fromIndex, 1);
      next.splice(toIndex, 0, item);
      return next;
    });
  };

  const updateFieldInBlock = (blockId: string, fieldId: string, patch: Partial<BuilderField>) => {
    setBlocks((prev) =>
      prev.map((b) => {
        if (b.id !== blockId || b.type !== 'fields') return b;
        return {
          ...b,
          fields: b.fields.map((f) => (f.id === fieldId ? { ...f, ...patch } : f)),
        };
      })
    );
  };

  const addFieldToBlock = (blockId: string) => {
    setBlocks((prev) =>
      prev.map((b) => {
        if (b.id !== blockId || b.type !== 'fields') return b;
        return {
          ...b,
          fields: [
            ...b.fields,
            { id: crypto.randomUUID(), section: b.title || 'Campos', key: '', label: '', type: 'text', width: 'half' },
          ],
        };
      })
    );
  };

  const removeFieldFromBlock = (blockId: string, fieldId: string) => {
    setBlocks((prev) =>
      prev.map((b) => {
        if (b.id !== blockId || b.type !== 'fields') return b;
        return { ...b, fields: b.fields.filter((f) => f.id !== fieldId) };
      })
    );
  };

  const updateTableColumn = (blockId: string, colIndex: number, value: string) => {
    setBlocks((prev) =>
      prev.map((b) => {
        if (b.id !== blockId || b.type !== 'table') return b;
        const columns = [...b.columns];
        columns[colIndex] = value;
        return { ...b, columns };
      })
    );
  };

  const addTableColumn = (blockId: string) => {
    setBlocks((prev) =>
      prev.map((b) => {
        if (b.id !== blockId || b.type !== 'table') return b;
        const nextColName = `Coluna ${b.columns.length + 1}`;
        return {
          ...b,
          columns: [...b.columns, nextColName],
          rows: b.rows.map((r) => [...r, '']),
        };
      })
    );
  };

  const removeTableColumn = (blockId: string, colIndex: number) => {
    setBlocks((prev) =>
      prev.map((b) => {
        if (b.id !== blockId || b.type !== 'table' || b.columns.length <= 1) return b;
        return {
          ...b,
          columns: b.columns.filter((_, i) => i !== colIndex),
          rows: b.rows.map((r) => r.filter((_, i) => i !== colIndex)),
        };
      })
    );
  };

  const updateTableCell = (blockId: string, rowIndex: number, colIndex: number, value: string) => {
    setBlocks((prev) =>
      prev.map((b) => {
        if (b.id !== blockId || b.type !== 'table') return b;
        const rows = b.rows.map((row, rIdx) => {
          if (rIdx !== rowIndex) return row;
          const next = [...row];
          next[colIndex] = value;
          return next;
        });
        return { ...b, rows };
      })
    );
  };

  const addTableRow = (blockId: string) => {
    setBlocks((prev) =>
      prev.map((b) => {
        if (b.id !== blockId || b.type !== 'table') return b;
        return {
          ...b,
          rows: [...b.rows, new Array(b.columns.length).fill('')],
        };
      })
    );
  };

  const removeTableRow = (blockId: string, rowIndex: number) => {
    setBlocks((prev) =>
      prev.map((b) => {
        if (b.id !== blockId || b.type !== 'table' || b.rows.length <= 1) return b;
        return { ...b, rows: b.rows.filter((_, i) => i !== rowIndex) };
      })
    );
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
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <button
            type="button"
            className={`btn text-sm ${mockMode ? 'btn-primary' : ''}`}
            onClick={() => setMockMode((prev) => !prev)}
          >
            {mockMode ? 'Modo mock/local ativo' : 'Ativar modo mock/local'}
          </button>
          <span className="text-xs text-kyx-500">
            No modo mock, salvar/editar/listar ocorre no navegador (localStorage), sem dependência de BD.
          </span>
        </div>
      </div>

      {error && <div className="p-3 rounded bg-danger-500/10 border border-danger-500/30 text-danger-500">{error}</div>}

      <div className="grid lg:grid-cols-12 gap-6">
        <div className="card p-5 space-y-4 lg:col-span-4">
          <h3 className="text-lg font-semibold text-white">Construtor de formulário</h3>
          <p className="text-sm text-kyx-400">
            Monte os campos e gere automaticamente o HTML + campos obrigatórios.
          </p>
          <div className="flex gap-2">
            <button
              type="button"
              className={`btn text-xs ${builderMode === 'structured' ? 'btn-primary' : ''}`}
              onClick={() => setBuilderMode('structured')}
            >
              Modo estruturado
            </button>
            <button
              type="button"
              className={`btn text-xs ${builderMode === 'blocks' ? 'btn-primary' : ''}`}
              onClick={() => setBuilderMode('blocks')}
            >
              Modo blocos (arrastar)
            </button>
          </div>

          <div className="p-3 rounded-lg border border-kyx-800/40 bg-kyx-900/30 space-y-3">
            <div className="flex items-center justify-between gap-2 flex-wrap">
              <p className="text-xs font-medium text-kyx-300">Cores do PDF</p>
              <button type="button" className="btn text-xs" onClick={() => setPdfTheme({ ...DEFAULT_PDF_THEME })}>
                Restaurar padrão
              </button>
            </div>
            <p className="text-xs text-kyx-500">
              Valem para o preview e para o HTML ao clicar em aplicar. O motor já imprime fundos (<code className="text-kyx-400">PrintBackground</code>).
            </p>
            <div className="grid grid-cols-2 gap-3">
              {PDF_THEME_CONTROLS.map(({ key, label }) => (
                <label key={key} className="flex flex-col gap-1.5 text-xs text-kyx-400">
                  <span>{label}</span>
                  <input
                    type="color"
                    className="h-9 w-full max-w-[7.5rem] rounded border border-kyx-700 bg-kyx-950 cursor-pointer"
                    value={pdfTheme[key]}
                    onChange={(e) => setPdfTheme((p) => ({ ...p, [key]: e.target.value }))}
                  />
                </label>
              ))}
            </div>
          </div>

          {builderMode === 'blocks' ? (
            <div className="space-y-3">
              <div className="p-3 rounded-lg border border-kyx-800/40 bg-kyx-900/30">
                <p className="text-xs text-kyx-400 mb-2">Paleta de blocos (clique para adicionar)</p>
                <div className="flex flex-wrap gap-2">
                  <button type="button" className="btn text-xs" onClick={() => addBlock('title')}>+ Cabeçalho</button>
                  <button type="button" className="btn text-xs" onClick={() => addBlock('fields')}>+ Campos</button>
                  <button type="button" className="btn text-xs" onClick={() => addBlock('table')}>+ Tabela</button>
                  <button type="button" className="btn text-xs" onClick={() => addBlock('twoCols')}>+ 2 colunas</button>
                  <button type="button" className="btn text-xs" onClick={() => addBlock('text')}>+ Texto</button>
                  <button type="button" className="btn text-xs" onClick={() => addBlock('signature')}>+ Assinatura</button>
                  <button type="button" className="btn text-xs" onClick={() => addBlock('divider')}>+ Divisor</button>
                </div>
              </div>

              <div className="space-y-2 max-h-[560px] overflow-auto pr-1">
                {blocks.map((block) => (
                  <div
                    key={block.id}
                    draggable
                    onDragStart={() => setDraggingBlockId(block.id)}
                    onDragOver={(e) => e.preventDefault()}
                    onDrop={() => {
                      if (draggingBlockId) moveBlock(draggingBlockId, block.id);
                      setDraggingBlockId(null);
                    }}
                    className="p-3 rounded-lg border border-kyx-800/40 bg-kyx-900/30 space-y-2"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <p className="text-xs text-kyx-400">Bloco: {block.type}</p>
                      <button type="button" className="btn text-xs" onClick={() => removeBlock(block.id)}>
                        Remover
                      </button>
                    </div>

                    {block.type === 'title' && (
                      <>
                        <input className="input" placeholder="Badge" value={block.badge} onChange={(e) => updateBlock(block.id, { badge: e.target.value })} />
                        <input className="input" placeholder="Título" value={block.title} onChange={(e) => updateBlock(block.id, { title: e.target.value })} />
                        <input className="input" placeholder="Subtítulo" value={block.subtitle} onChange={(e) => updateBlock(block.id, { subtitle: e.target.value })} />
                      </>
                    )}

                    {block.type === 'text' && (
                      <>
                        <input className="input" placeholder="Título da seção" value={block.title} onChange={(e) => updateBlock(block.id, { title: e.target.value })} />
                        <textarea className="input min-h-[90px]" placeholder="Texto do bloco" value={block.text} onChange={(e) => updateBlock(block.id, { text: e.target.value })} />
                      </>
                    )}

                    {block.type === 'twoCols' && (
                      <>
                        <input className="input" placeholder="Título esquerda" value={block.leftTitle} onChange={(e) => updateBlock(block.id, { leftTitle: e.target.value })} />
                        <textarea className="input min-h-[70px]" placeholder="Conteúdo esquerda" value={block.leftText} onChange={(e) => updateBlock(block.id, { leftText: e.target.value })} />
                        <input className="input" placeholder="Título direita" value={block.rightTitle} onChange={(e) => updateBlock(block.id, { rightTitle: e.target.value })} />
                        <textarea className="input min-h-[70px]" placeholder="Conteúdo direita" value={block.rightText} onChange={(e) => updateBlock(block.id, { rightText: e.target.value })} />
                      </>
                    )}

                    {block.type === 'signature' && (
                      <>
                        <input className="input" placeholder="Label da assinatura" value={block.label} onChange={(e) => updateBlock(block.id, { label: e.target.value })} />
                        <input className="input font-mono" placeholder="Chave do nome (ex: assinaturaNome)" value={block.nameKey} onChange={(e) => updateBlock(block.id, { nameKey: e.target.value.replace(/\s+/g, '') })} />
                        <input className="input font-mono" placeholder="Chave da data (ex: dataAssinatura)" value={block.dateKey} onChange={(e) => updateBlock(block.id, { dateKey: e.target.value.replace(/\s+/g, '') })} />
                      </>
                    )}

                    {block.type === 'fields' && (
                      <div className="space-y-2">
                        <input className="input" placeholder="Título da seção" value={block.title} onChange={(e) => updateBlock(block.id, { title: e.target.value })} />
                        <select className="input" value={block.layout} onChange={(e) => updateBlock(block.id, { layout: e.target.value as 'table' | 'cards' })}>
                          <option value="cards">Cards</option>
                          <option value="table">Tabela</option>
                        </select>
                        {block.fields.map((field) => (
                          <div key={field.id} className="p-2 rounded border border-kyx-800/40 bg-kyx-950/40 space-y-2">
                            <input className="input" placeholder="Label" value={field.label} onChange={(e) => updateFieldInBlock(block.id, field.id, { label: e.target.value })} />
                            <input className="input font-mono" placeholder="Chave" value={field.key} onChange={(e) => updateFieldInBlock(block.id, field.id, { key: e.target.value.replace(/\s+/g, '') })} />
                            <div className="grid grid-cols-2 gap-2">
                              <select className="input" value={field.type} onChange={(e) => updateFieldInBlock(block.id, field.id, { type: e.target.value as FieldType })}>
                                <option value="text">Texto</option>
                                <option value="email">E-mail</option>
                                <option value="date">Data</option>
                                <option value="phone">Telefone</option>
                                <option value="checkbox">Checkbox</option>
                              </select>
                              <select className="input" value={field.width} onChange={(e) => updateFieldInBlock(block.id, field.id, { width: e.target.value as 'full' | 'half' })}>
                                <option value="half">Meia largura</option>
                                <option value="full">Largura total</option>
                              </select>
                            </div>
                            <button type="button" className="btn text-xs" onClick={() => removeFieldFromBlock(block.id, field.id)}>
                              Remover campo
                            </button>
                          </div>
                        ))}
                        <button type="button" className="btn text-xs" onClick={() => addFieldToBlock(block.id)}>
                          + Campo no bloco
                        </button>
                      </div>
                    )}

                    {block.type === 'table' && (
                      <div className="space-y-2">
                        <input
                          className="input"
                          placeholder="Título da tabela"
                          value={block.title}
                          onChange={(e) => updateBlock(block.id, { title: e.target.value })}
                        />
                        <div className="grid grid-cols-1 gap-2">
                          {block.columns.map((col, colIndex) => (
                            <div key={`${block.id}-col-${colIndex}`} className="flex gap-2">
                              <input
                                className="input"
                                placeholder={`Nome da coluna ${colIndex + 1}`}
                                value={col}
                                onChange={(e) => updateTableColumn(block.id, colIndex, e.target.value)}
                              />
                              <button
                                type="button"
                                className="btn text-xs"
                                onClick={() => removeTableColumn(block.id, colIndex)}
                              >
                                Remover
                              </button>
                            </div>
                          ))}
                        </div>
                        <button type="button" className="btn text-xs" onClick={() => addTableColumn(block.id)}>
                          + Coluna
                        </button>
                        <div className="space-y-2">
                          {block.rows.map((row, rowIndex) => (
                            <div key={`${block.id}-row-${rowIndex}`} className="p-2 rounded border border-kyx-800/40 bg-kyx-950/40 space-y-2">
                              <div className="flex items-center justify-between">
                                <p className="text-xs text-kyx-400">Linha {rowIndex + 1}</p>
                                <button
                                  type="button"
                                  className="btn text-xs"
                                  onClick={() => removeTableRow(block.id, rowIndex)}
                                >
                                  Remover linha
                                </button>
                              </div>
                              <div className="grid grid-cols-1 gap-2">
                                {row.map((cell, colIndex) => (
                                  <input
                                    key={`${block.id}-cell-${rowIndex}-${colIndex}`}
                                    className="input font-mono"
                                    placeholder={`Célula ${rowIndex + 1}.${colIndex + 1} (texto ou {{chave}})`}
                                    value={cell}
                                    onChange={(e) => updateTableCell(block.id, rowIndex, colIndex, e.target.value)}
                                  />
                                ))}
                              </div>
                            </div>
                          ))}
                        </div>
                        <button type="button" className="btn text-xs" onClick={() => addTableRow(block.id)}>
                          + Linha
                        </button>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          ) : (
            <>
              <div className="space-y-3">
                <div>
                  <label className="block text-xs text-kyx-500 mb-1">Título do documento</label>
                  <input className="input" value={builderTitle} onChange={(e) => setBuilderTitle(e.target.value)} />
                </div>
                <div>
                  <label className="block text-xs text-kyx-500 mb-1">Subtítulo</label>
                  <input className="input" value={builderSubtitle} onChange={(e) => setBuilderSubtitle(e.target.value)} />
                </div>
                <div>
                  <label className="block text-xs text-kyx-500 mb-1">Badge</label>
                  <input className="input" value={builderBadge} onChange={(e) => setBuilderBadge(e.target.value)} />
                </div>
                <div>
                  <label className="block text-xs text-kyx-500 mb-1">Layout do formulário</label>
                  <select className="input" value={layoutStyle} onChange={(e) => setLayoutStyle(e.target.value as 'table' | 'cards')}>
                    <option value="cards">Cards flexíveis (lado a lado)</option>
                    <option value="table">Tabela clássica</option>
                  </select>
                </div>
              </div>

              <div className="space-y-2 max-h-[420px] overflow-auto pr-1">
                {builderFields.map((field, index) => (
                  <div key={field.id} className="p-3 rounded-lg border border-kyx-800/40 bg-kyx-900/30 space-y-2">
                    <p className="text-xs text-kyx-400">Campo #{index + 1}</p>
                    <input
                      className="input"
                      placeholder="Seção (ex: Dados pessoais)"
                      value={field.section}
                      onChange={(e) => updateBuilderField(field.id, { section: e.target.value })}
                    />
                    <input
                      className="input"
                      placeholder="Label (ex: Nome completo)"
                      value={field.label}
                      onChange={(e) => updateBuilderField(field.id, { label: e.target.value })}
                    />
                    <input
                      className="input font-mono"
                      placeholder="Chave (ex: nomeCompleto)"
                      value={field.key}
                      onChange={(e) => updateBuilderField(field.id, { key: e.target.value.replace(/\s+/g, '') })}
                    />
                    <select
                      className="input"
                      value={field.type}
                      onChange={(e) => updateBuilderField(field.id, { type: e.target.value as FieldType })}
                    >
                      <option value="text">Texto</option>
                      <option value="email">E-mail</option>
                      <option value="date">Data</option>
                      <option value="phone">Telefone</option>
                      <option value="checkbox">Checkbox (X / vazio)</option>
                    </select>
                    <select
                      className="input"
                      value={field.width}
                      onChange={(e) => updateBuilderField(field.id, { width: e.target.value as 'full' | 'half' })}
                    >
                      <option value="half">Meia largura (lado a lado)</option>
                      <option value="full">Largura total</option>
                    </select>
                    <button type="button" className="btn text-sm" onClick={() => removeBuilderField(field.id)}>
                      Remover campo
                    </button>
                  </div>
                ))}
              </div>
            </>
          )}

          <div className="flex flex-wrap gap-2">
            {builderMode === 'structured' && (
              <>
                <button type="button" className="btn" onClick={addBuilderField}>
                  + Campo
                </button>
                <button type="button" className="btn" onClick={() => setBuilderFields(createDefaultBuilderFields())}>
                  Campos padrão
                </button>
              </>
            )}
            <button type="button" className="btn btn-primary" onClick={applyBuilderToTemplate}>
              {builderMode === 'blocks' ? 'Aplicar blocos no formulário' : 'Gerar modelo no formulário'}
            </button>
          </div>
        </div>

        <div className="card p-4 lg:col-span-8 space-y-3 lg:sticky lg:top-6 h-fit">
          <div className="flex items-center justify-between gap-2">
            <p className="text-base text-white font-semibold">Visualização ao vivo (modo TV)</p>
            <div className="flex gap-2">
              <button
                type="button"
                className={`btn text-xs ${previewMode === 'sample' ? 'btn-primary' : ''}`}
                onClick={() => setPreviewMode('sample')}
              >
                Dados de exemplo
              </button>
              <button
                type="button"
                className={`btn text-xs ${previewMode === 'placeholder' ? 'btn-primary' : ''}`}
                onClick={() => setPreviewMode('placeholder')}
              >
                Placeholders
              </button>
            </div>
          </div>
          <p className="text-xs text-kyx-400">
            Vá adicionando os campos no construtor e veja o formulário sendo montado aqui em tempo real.
          </p>
          <div className="rounded-xl border border-kyx-700/40 bg-kyx-950/70 p-3">
            <div className="w-full h-[72vh] min-h-[620px] rounded-lg overflow-hidden bg-white">
              <iframe
                title="Preview template TV"
                srcDoc={previewHtml}
                className="w-full h-full border-0 bg-white"
              />
            </div>
          </div>
          <div className="p-3 rounded-lg border border-kyx-800/40 bg-kyx-900/30">
            <p className="text-xs text-kyx-400 mb-1">
              Campos detectados (
              {builderMode === 'blocks' ? generatedFromBlocks.required.length : generatedFromBuilder.required.length})
            </p>
            <p className="text-xs text-kyx-300 font-mono break-all">
              {(builderMode === 'blocks' ? generatedFromBlocks.required : generatedFromBuilder.required).join(', ') ||
                'Nenhum campo válido ainda.'}
            </p>
          </div>
        </div>
      </div>

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
