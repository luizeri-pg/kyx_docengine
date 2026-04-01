namespace KYX.DocEngine.API.Models.DTOs.Dashboard;

/// <summary>Linha de <c>tb_log_requisicao</c> para listagens (histórico / dashboard).</summary>
public class LogRequisicaoListItemDto
{
    public string Id { get; set; } = null!;
    public string RequisicaoId { get; set; } = null!;
    public string? UsuarioId { get; set; }
    public string? Canal { get; set; }
    public string? CentroCusto { get; set; }
    public int? StatusHttp { get; set; }
    public int? TempoRespostaMs { get; set; }
    public string? Erro { get; set; }
    public DateTime CriadoEm { get; set; }
}

public class LogIntegracaoItemDto
{
    public string Id { get; set; } = null!;
    public string RequisicaoId { get; set; } = null!;
    public string IntegracaoId { get; set; } = null!;
    public string? Endpoint { get; set; }
    public string? Metodo { get; set; }
    public int? StatusHttp { get; set; }
    public int? TempoRespostaMs { get; set; }
    public DateTime CriadoEm { get; set; }
}

public class LogDetalheDto
{
    public LogRequisicaoListItemDto Requisicao { get; set; } = null!;
    public IReadOnlyList<LogIntegracaoItemDto> Integracoes { get; set; } = Array.Empty<LogIntegracaoItemDto>();
}
