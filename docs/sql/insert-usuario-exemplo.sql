-- =============================================================================
-- Inserir utilizador em tb_usuario (login via POST /auth/login)
-- Ajuste nomes de colunas se o teu schema for diferente (ver Schema em appsettings).
--
-- Se tiveres erro 42703 "column id does not exist": a tua tabela pode usar outro
-- nome para a PK (ex.: usuario_id). Lista as colunas:
--   SELECT column_name FROM information_schema.columns
--   WHERE table_schema = 'public' AND table_name = 'tb_usuario';
-- Depois substitui "id" no INSERT abaixo pelo nome real e alinha Schema:Usuario:Id
-- na API (appsettings.Local.json). Ver também: consultar-tb_usuario.sql
-- =============================================================================

-- 1) Garantir um perfil (usa o primeiro existente; ou cria um mínimo se a tabela estiver vazia)
INSERT INTO tb_perfil (id, nome, descricao, criado_em, atualizado_em)
SELECT 'perfil-docengine-demo', 'DocEngine', 'Perfil criado pelo script de exemplo', timezone('utc', now()), timezone('utc', now())
WHERE NOT EXISTS (SELECT 1 FROM tb_perfil LIMIT 1);

-- Se já existir qualquer perfil, o INSERT acima não corre; usa o primeiro id:
-- SELECT id FROM tb_perfil LIMIT 1;

-- 2) Gerar hash BCrypt da senha (na máquina de dev):
--    cd backend/tools/HashPassword && dotnet run -- "SUA_SENHA"
--    Copia a linha que começa por $2a$ ou $2b$ para o campo senha abaixo.

-- 3) Substituir HASH_BCRYPT_AQUI e ajustar email/nome/login conforme o caso.
INSERT INTO tb_usuario (
    id,
    nome,
    email,
    senha,
    perfil_id,
    ativo,
    criado_em,
    atualizado_em
)
VALUES (
    gen_random_uuid()::text,
    'Utilizador DocEngine',
    '98765432100.0001',
    'HASH_BCRYPT_AQUI',
    (SELECT id FROM tb_perfil LIMIT 1),
    true,
    timezone('utc', now()),
    timezone('utc', now())
)
ON CONFLICT (email) DO NOTHING;

-- Nota: se o email já existir, o INSERT é ignorado (índice único em email).
-- O login usa username = email OU nome; aqui o "username" do cliente pode ser o valor em email.
