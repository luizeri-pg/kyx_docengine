-- =============================================================================
-- INSERIR utilizador de exemplo na tb_usuario (schema legado KYX / Notify)
--
-- !!! OBRIGATÓRIO PARA O LOGIN FUNCIONAR !!!
-- Copie backend/KYX.DocEngine.API/appsettings.Local.json.example para
-- appsettings.Local.json e preencha ConnectionStrings + bloco Schema:Usuario
-- (Id=id_usuario, Login=str_login, Senha=str_senha, Ativo=bloqueado, AtivoInverted=true, etc.).
-- Sem isto, a API usa o mapeamento por defeito (colunas id/nome/email) e NÃO lê str_login —
-- o POST com "username":"docengine.demo" devolve sempre 401 «Credenciais inválidas».
--
-- DADOS DESTE EXEMPLO (para testar POST /auth/login na API):
--   id_usuario ........... 999999  (altera se este ID já existir na tua BD)
--   str_login ............ docengine.demo   → usa como "username" no JSON
--   str_descricao ........ Utilizador DocEngine Demo
--   senha em texto plano . DocEngine@2025   (só documentação — na BD fica o hash BCrypt)
--
-- Hash BCrypt gerado com: dotnet run --project backend/tools/HashPassword -- "DocEngine@2025"
--
-- ANTES DE EXECUTAR: confirma colunas com:
--   SELECT column_name FROM information_schema.columns
--   WHERE table_schema = 'public' AND table_name = 'tb_usuario' ORDER BY ordinal_position;
--
-- Se der erro de permissão no schema "log" (trigger), pede GRANT ao DBA ou usa user admin.
-- =============================================================================

INSERT INTO tb_usuario (
    id_usuario,
    str_ativo,
    id_usuarioinclui,
    dh_inclui,
    id_usuarioedita,
    dh_edita,
    str_descricao,
    str_login,
    str_senha,
    bloqueado,
    email
) VALUES (
    999999,
    'A',
    1,
    timezone('utc', now()),
    1,
    timezone('utc', now()),
    'Utilizador DocEngine Demo',
    'docengine.demo',
    '$2a$11$8ZObJqFLV2iQ13xmALf3HeWAO/G.SajoTaxGsCXVJzck9P3.4wTmi',
    false,
    NULL
);

-- Se o id 999999 já existir, apaga a linha acima e usa por exemplo:
-- id_usuario: (SELECT COALESCE(MAX(id_usuario), 0) + 1 FROM tb_usuario)

-- Login na API (exemplo curl):
-- curl -s -X POST http://localhost:3000/auth/login \
--   -H "Content-Type: application/json" \
--   -d '{"username":"docengine.demo","password":"DocEngine@2025"}'
