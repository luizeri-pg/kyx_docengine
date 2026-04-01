-- =====================================================================
-- DocEngine — tabelas PDF: templates + document_jobs
-- =====================================================================
-- Estado final alinhado às migrações EF:
--   InitialCreate (templates + document_jobs)
--   DocumentJobInlineTemplateSnapshot (template_id NULL + template_snapshot_json + FK SET NULL)
--
-- Uso: executar no PostgreSQL do ambiente (ex.: doc_engine_dev), schema public.
-- Não recria nem altera tabelas tb_* legadas.
--
-- Permissões: após criar, conceder ao usuário da API (ex.: doc_engine):
--   GRANT SELECT, INSERT, UPDATE, DELETE ON public.templates TO doc_engine;
--   GRANT SELECT, INSERT, UPDATE, DELETE ON public.document_jobs TO doc_engine;
-- =====================================================================

CREATE TABLE IF NOT EXISTS public.templates (
    id uuid NOT NULL,
    slug text NOT NULL,
    name text NOT NULL,
    type text NOT NULL,
    content text NOT NULL,
    required_fields text NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_templates PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_templates_slug ON public.templates (slug);

CREATE TABLE IF NOT EXISTS public.document_jobs (
    id uuid NOT NULL,
    requisicao_id text NOT NULL,
    template_id uuid NULL,
    template_snapshot_json text NULL,
    centro_custo text NOT NULL,
    nome_arquivo text NOT NULL,
    input_data text NOT NULL,
    status text NOT NULL,
    result_base64 text NULL,
    error_message text NULL,
    processing_time_ms bigint NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_document_jobs PRIMARY KEY (id),
    CONSTRAINT fk_document_jobs_templates_template_id FOREIGN KEY (template_id) REFERENCES public.templates (id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_document_jobs_requisicao_id ON public.document_jobs (requisicao_id);
CREATE INDEX IF NOT EXISTS ix_document_jobs_template_id ON public.document_jobs (template_id);

-- =====================================================================
-- Se document_jobs já existir (versão antiga sem inline template), ajustar:
-- =====================================================================
-- ALTER TABLE public.document_jobs DROP CONSTRAINT IF EXISTS fk_document_jobs_templates_template_id;
-- ALTER TABLE public.document_jobs ALTER COLUMN template_id DROP NOT NULL;
-- ALTER TABLE public.document_jobs ADD COLUMN IF NOT EXISTS template_snapshot_json text NULL;
-- ALTER TABLE public.document_jobs ADD CONSTRAINT fk_document_jobs_templates_template_id
--   FOREIGN KEY (template_id) REFERENCES public.templates (id) ON DELETE SET NULL;
