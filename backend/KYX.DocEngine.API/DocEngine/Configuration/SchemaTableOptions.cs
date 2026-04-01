namespace KYX.DocEngine.API.Configuration;

/// <summary>
/// Nomes reais das colunas no PostgreSQL quando o schema legado difere do modelo Notify padrão.
/// Ajuste em <c>appsettings.Local.json</c> conforme <c>\d tb_usuario</c> / <c>\d tb_log_requisicao</c>.
/// </summary>
public class SchemaTableOptions
{
    public UsuarioColumnOptions Usuario { get; set; } = new();
    public PerfilColumnOptions Perfil { get; set; } = new();
    public LogRequisicaoColumnOptions LogRequisicao { get; set; } = new();
}

public class UsuarioColumnOptions
{
    public string Id { get; set; } = "id";
    public string Nome { get; set; } = "nome";
    public string Email { get; set; } = "email";
    /// <summary>Coluna de login legada (ex.: str_login). Vazio = não mapeado.</summary>
    public string Login { get; set; } = "";
    public string Senha { get; set; } = "senha";
    /// <summary>Vazio = tabela sem <c>perfil_id</c>; a FK para <c>tb_perfil</c> não é mapeada.</summary>
    public string PerfilId { get; set; } = "perfil_id";
    public string Ativo { get; set; } = "ativo";
    /// <summary>
    /// Se <c>true</c>, o valor na coluna <see cref="Ativo"/> é invertido (ex.: coluna <c>bloqueado</c>:
    /// utilizador ativo em C# = <c>false</c> na BD).
    /// </summary>
    public bool AtivoInverted { get; set; } = false;
    public string CriadoEm { get; set; } = "criado_em";
    public string AtualizadoEm { get; set; } = "atualizado_em";
    /// <summary>Se <c>true</c>, a PK na BD é <c>integer</c> (ex.: id_usuario); em memória continua <c>string</c>.</summary>
    public bool IdIntegerType { get; set; } = false;
    /// <summary>Se <c>false</c>, o índice em email não é único (bases legadas).</summary>
    public bool UniqueEmailIndex { get; set; } = true;
}

public class PerfilColumnOptions
{
    public string Id { get; set; } = "id";
    public string Nome { get; set; } = "nome";
    /// <summary>Nome real da coluna 'descricao'. Vazio = ignorada (sem coluna na tabela legada).</summary>
    public string Descricao { get; set; } = "descricao";
    public string CriadoEm { get; set; } = "criado_em";
    /// <summary>
    /// Coluna de atualização. Se for igual a <see cref="CriadoEm"/>, o EF ignora <c>AtualizadoEm</c> no modelo
    /// (uma coluna só — típico em <c>tb_perfil</c> legado).
    /// </summary>
    public string AtualizadoEm { get; set; } = "atualizado_em";
    /// <summary>Se true, a PK na BD é integer; em memória continua string.</summary>
    public bool IdIntegerType { get; set; } = false;
}

public class LogRequisicaoColumnOptions
{
    public string Id { get; set; } = "id";
    public string RequisicaoId { get; set; } = "requisicao_id";
    public string UsuarioId { get; set; } = "usuario_id";
    /// <summary>Se vazio, a propriedade <c>Canal</c> não é mapeada (evita INSERT quando a coluna não existe).</summary>
    public string Canal { get; set; } = "canal";
    public string CentroCusto { get; set; } = "centro_custo";
    public string RequestPayload { get; set; } = "request_payload";
    public string ResponsePayload { get; set; } = "response_payload";
    public string StatusHttp { get; set; } = "status_http";
    public string TempoRespostaMs { get; set; } = "tempo_resposta_ms";
    public string Erro { get; set; } = "erro";
    public string CriadoEm { get; set; } = "criado_em";
}
