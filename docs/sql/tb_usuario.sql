-- Esquema alinhado ao modelo Notify/KYX / DocEngine (login em tb_usuario).
-- A migração EF MapUsuarioToTbUsuario aplica o equivalente com IF NOT EXISTS.

CREATE TABLE IF NOT EXISTS tb_usuario (
    id text NOT NULL,
    nome text NOT NULL,
    email text NOT NULL,
    senha text NOT NULL,
    perfil_id text NOT NULL,
    ativo boolean NOT NULL DEFAULT TRUE,
    criado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
    atualizado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
    CONSTRAINT pk_tb_usuario PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_tb_usuario_email ON tb_usuario (email);
