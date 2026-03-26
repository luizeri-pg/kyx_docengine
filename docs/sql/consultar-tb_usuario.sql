-- =============================================================================
-- Consultar utilizador SEM assumir o nome da coluna PK (pode ser id, usuario_id, etc.)
-- =============================================================================

-- 1) Descobre os nomes das colunas em tb_usuario (executa primeiro se tiveres erro 42703)
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'tb_usuario'
ORDER BY ordinal_position;

-- 2) Procura pelo login/email (não usa "id" no SELECT)
SELECT *
FROM tb_usuario
WHERE email = '98765432100.0001'
   OR nome = '98765432100.0001';

-- 2b) Schema legado típico (str_login / str_senha / bloqueado / id_usuario):
-- SELECT *
-- FROM tb_usuario
-- WHERE str_login = '98765432100.0001'
--    OR email = '98765432100.0001'
--    OR str_descricao = '98765432100.0001';

-- Depois de veres o nome da PK na query (1), podes usar por exemplo:
-- SELECT usuario_id, nome, email, ativo FROM tb_usuario WHERE ...;
-- (substitui usuario_id pelo nome real que aparecer em information_schema)
