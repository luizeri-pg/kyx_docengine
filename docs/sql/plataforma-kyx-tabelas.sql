-- Schema plataforma KYX / Notify (idempotente).
-- A migração EF MapPlataformaKyxTabelas aplica a mesma lógica.
-- Ordem: tb_perfil, tb_role → tb_perfil_role → tb_integracao → tb_template → tb_log_* / tb_consumo → FK tb_usuario.

DROP INDEX IF EXISTS ix_tb_log_requisicao_requisicao_id;
CREATE UNIQUE INDEX IF NOT EXISTS ix_tb_log_requisicao_requisicao_id ON tb_log_requisicao (requisicao_id);

CREATE TABLE IF NOT EXISTS tb_perfil (
    id text NOT NULL,
    nome text NOT NULL,
    descricao text NULL,
    criado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
    atualizado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
    CONSTRAINT pk_tb_perfil PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_tb_perfil_nome ON tb_perfil (nome);

CREATE TABLE IF NOT EXISTS tb_role (
    id text NOT NULL,
    nome text NOT NULL,
    descricao text NULL,
    criado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
    atualizado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
    CONSTRAINT pk_tb_role PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_tb_role_nome ON tb_role (nome);

CREATE TABLE IF NOT EXISTS tb_perfil_role (
    perfil_id text NOT NULL,
    role_id text NOT NULL,
    criado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
    CONSTRAINT pk_tb_perfil_role PRIMARY KEY (perfil_id, role_id),
    CONSTRAINT fk_tb_perfil_role_tb_perfil FOREIGN KEY (perfil_id) REFERENCES tb_perfil (id) ON DELETE CASCADE,
    CONSTRAINT fk_tb_perfil_role_tb_role FOREIGN KEY (role_id) REFERENCES tb_role (id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_tb_perfil_role_role_id ON tb_perfil_role (role_id);

CREATE TABLE IF NOT EXISTS tb_integracao (
    id text NOT NULL,
    nome text NOT NULL,
    descricao text NULL,
    tipo text NOT NULL,
    canal text NOT NULL,
    provedor text NOT NULL,
    url_base text NULL,
    credenciais text NOT NULL DEFAULT '{}',
    ativo boolean NOT NULL DEFAULT TRUE,
    criado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
    atualizado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
    CONSTRAINT pk_tb_integracao PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS tb_template (
    id text NOT NULL,
    nome text NOT NULL,
    tipo text NOT NULL,
    canal text NOT NULL,
    conteudo_html text NULL,
    variaveis jsonb NULL,
    ativo boolean NOT NULL DEFAULT TRUE,
    criado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
    atualizado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
    CONSTRAINT pk_tb_template PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS tb_log_integracao (
    id text NOT NULL,
    requisicao_id text NOT NULL,
    integracao_id text NOT NULL,
    endpoint text NULL,
    metodo text NULL,
    status_http integer NULL,
    request_headers jsonb NULL,
    request_body jsonb NULL,
    response_headers jsonb NULL,
    response_body jsonb NULL,
    tempo_resposta_ms integer NULL,
    criado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
    CONSTRAINT pk_tb_log_integracao PRIMARY KEY (id),
    CONSTRAINT fk_tb_log_integracao_tb_integracao FOREIGN KEY (integracao_id) REFERENCES tb_integracao (id) ON DELETE RESTRICT,
    CONSTRAINT fk_tb_log_integracao_tb_log_requisicao FOREIGN KEY (requisicao_id) REFERENCES tb_log_requisicao (requisicao_id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_tb_log_integracao_requisicao_id ON tb_log_integracao (requisicao_id);
CREATE INDEX IF NOT EXISTS ix_tb_log_integracao_integracao_id ON tb_log_integracao (integracao_id);
CREATE INDEX IF NOT EXISTS ix_tb_log_integracao_criado_em ON tb_log_integracao (criado_em);

CREATE TABLE IF NOT EXISTS tb_consumo (
    id text NOT NULL,
    requisicao_id text NOT NULL,
    integracao_id text NOT NULL,
    centro_custo text NOT NULL,
    canal text NOT NULL,
    valor numeric(10,2) NULL,
    criado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
    CONSTRAINT pk_tb_consumo PRIMARY KEY (id),
    CONSTRAINT fk_tb_consumo_tb_integracao FOREIGN KEY (integracao_id) REFERENCES tb_integracao (id) ON DELETE RESTRICT,
    CONSTRAINT fk_tb_consumo_tb_log_requisicao FOREIGN KEY (requisicao_id) REFERENCES tb_log_requisicao (requisicao_id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_tb_consumo_requisicao_id ON tb_consumo (requisicao_id);
CREATE INDEX IF NOT EXISTS ix_tb_consumo_integracao_id ON tb_consumo (integracao_id);
CREATE INDEX IF NOT EXISTS ix_tb_consumo_centro_custo ON tb_consumo (centro_custo);
CREATE INDEX IF NOT EXISTS ix_tb_consumo_canal ON tb_consumo (canal);
CREATE INDEX IF NOT EXISTS ix_tb_consumo_criado_em ON tb_consumo (criado_em);

CREATE INDEX IF NOT EXISTS ix_tb_usuario_perfil_id ON tb_usuario (perfil_id);

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'fk_tb_usuario_tb_perfil_perfil_id'
  ) THEN
    ALTER TABLE tb_usuario
      ADD CONSTRAINT fk_tb_usuario_tb_perfil_perfil_id
      FOREIGN KEY (perfil_id) REFERENCES tb_perfil (id) ON DELETE RESTRICT;
  END IF;
END $$;
