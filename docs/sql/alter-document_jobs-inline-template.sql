-- Opcional: aplicar manualmente se não usar `dotnet ef database update`
-- para suportar geração com template inline (sem linha na tabela `templates`).
-- Ajuste o nome do schema se não for o público.

ALTER TABLE document_jobs DROP CONSTRAINT IF EXISTS fk_document_jobs_templates_template_id;

ALTER TABLE document_jobs
  ALTER COLUMN template_id DROP NOT NULL;

ALTER TABLE document_jobs
  ADD COLUMN IF NOT EXISTS template_snapshot_json text NULL;

ALTER TABLE document_jobs
  ADD CONSTRAINT fk_document_jobs_templates_template_id
    FOREIGN KEY (template_id) REFERENCES templates (id)
    ON DELETE SET NULL;
