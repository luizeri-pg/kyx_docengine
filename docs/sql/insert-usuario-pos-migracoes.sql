-- =============================================================================
-- Utilizador de desenvolvimento após migrações EF (tb_usuario moderna)
-- Compatível com Schema em appsettings.json / appsettings.Local.json.example.fresh-db
--
-- Login na API:
--   username: docengine.demo   (valor em "nome" — também pode usar o email)
--   password: DocEngine@2025
--
-- Para regenerar o hash BCrypt da senha:
--   cd backend/tools/HashPassword && dotnet run -- "DocEngine@2025"
-- =============================================================================

INSERT INTO tb_perfil (id, nome, descricao, criado_em, atualizado_em)
SELECT 'perfil-docengine-demo', 'DocEngine', 'Perfil demo setup do zero', timezone('utc', now()), timezone('utc', now())
WHERE NOT EXISTS (SELECT 1 FROM tb_perfil WHERE id = 'perfil-docengine-demo');

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
    'docengine.demo',
    'docengine.demo@local.docengine',
    '$2a$11$lrhFHZIXAlfcUHvnVcLBBeh/FFqkcqCYQpTTtRleQPC4n4LTCo0d.',
    'perfil-docengine-demo',
    true,
    timezone('utc', now()),
    timezone('utc', now())
)
ON CONFLICT (email) DO NOTHING;
