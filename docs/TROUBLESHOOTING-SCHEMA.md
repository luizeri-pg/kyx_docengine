# Schema PostgreSQL (tb_usuario / tb_log_requisicao)

## Erro `column ... does not exist` (42703)

O modelo EF assume nomes de colunas (ex.: `id`, `canal`). Se a base legada usar outros nomes, defina em **`appsettings.Local.json`** (ou variáveis de ambiente) a secção **`Schema`**:

```json
{
  "Schema": {
    "Usuario": {
      "Id": "nome_da_pk_na_tabela",
      "Nome": "nome",
      "Email": "email",
      "Senha": "senha",
      "PerfilId": "perfil_id",
      "Ativo": "ativo",
      "CriadoEm": "criado_em",
      "AtualizadoEm": "atualizado_em"
    },
    "LogRequisicao": {
      "Canal": ""
    }
  }
}
```

- **`Schema:LogRequisicao:Canal` vazio** — a propriedade `Canal` não é persistida (útil quando a tabela **não tem** essa coluna). Em **Development** já vem vazio em `appsettings.Development.json`.

### `tb_usuario` legada (ex.: `id_usuario`, `str_login`, `str_senha`, `bloqueado`, sem `perfil_id`)

Se a tabela tiver colunas como no ecossistema antigo, usa **`appsettings.Local.json`** com algo deste género (ajusta nomes se forem diferentes):

```json
{
  "Schema": {
    "Usuario": {
      "Id": "id_usuario",
      "Nome": "str_descricao",
      "Email": "email",
      "Login": "str_login",
      "Senha": "str_senha",
      "PerfilId": "",
      "Ativo": "bloqueado",
      "AtivoInverted": true,
      "CriadoEm": "dh_inclui",
      "AtualizadoEm": "dh_edita",
      "IdIntegerType": true,
      "UniqueEmailIndex": false
    }
  }
}
```

- **`AtivoInverted: true`** — a coluna na BD é **`bloqueado`**: utilizador “ativo” no DocEngine = `bloqueado = false` na BD.
- **`PerfilId` vazio** — não mapeia FK para `tb_perfil` (tabela sem `perfil_id`).
- **`Login`** — o `username` no `POST /auth/login` pode ser **`str_login`**, `email`, `str_descricao` (Nome) ou o **`id_usuario`** em texto (ex.: `"301"`), desde que `Schema:Usuario:IdIntegerType` esteja `true` e o mapeamento de `Id` correto.
- **`IdIntegerType: true`** — PK inteira (`id_usuario`) mapeada como `string` no JWT. O login por **`username` igual ao ID** só é tentado se o valor for um **inteiro válido** (ex.: `301`). Valores como **`str_login`** (`98765432100.0001`) não são comparados com a PK (evita `FormatException` no conversor EF).
- A coluna **`str_ativo`** (texto) não é mapeada por defeito; se a regra de negócio depender dela em vez de `bloqueado`, alinha com o DBA ou estende o mapeamento.

### Descobrir nomes reais das colunas

No `psql`:

```sql
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'tb_usuario'
ORDER BY ordinal_position;
```

## Login enquanto o schema não está alinhado

1. **Alinhe `Schema:Usuario`** com os nomes reais das colunas em `tb_usuario` (ver acima). O login **normal** é só pela BD (`Auth:UseAllowedLoginsFallback`: false).
2. **Opcional (só dev):** `Auth:UseAllowedLoginsFallback`: true e `Auth:AllowedLogins` — apenas se precisares de entrar sem PostgreSQL; **não** usar em produção com utilizadores fixos.
3. **`Logging:PersistTbLogRequisicao`: false** — desliga gravação em `tb_log_requisicao` se a tabela/colunas forem diferentes. Em **Development** o padrão é `false`.

## Teste rápido com curl

O utilizador tem de existir em **`tb_usuario`** (email/nome + senha BCrypt) e o **`Schema`** tem de bater com as colunas reais.

```bash
curl -s -X POST http://localhost:3000/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"SEU_LOGIN_OU_EMAIL","password":"SUA_SENHA"}' | jq .
```

Resposta esperada: `sucesso: true` e `resultado.access_token` (JWT).
