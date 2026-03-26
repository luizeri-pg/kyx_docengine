namespace KYX.NotifyHUB.API.Models.DTOs.Dashboard;

public class HistoryQueryParams
{
    public string? Canal { get; set; }
    public string? CentroCusto { get; set; }
    public int Limit { get; set; } = 100;
    public int Offset { get; set; } = 0;
}

public class LogRequisicaoDto
{
    public string Id { get; set; } = string.Empty;
    public string RequisicaoId { get; set; } = string.Empty;
    public string? UsuarioId { get; set; }
    public string Canal { get; set; } = string.Empty;
    public string? CentroCusto { get; set; }
    public object? RequestPayload { get; set; }
    public object? ResponsePayload { get; set; }
    public int? StatusHttp { get; set; }
    public int? TempoRespostaMs { get; set; }
    public string? Erro { get; set; }
    public DateTime CriadoEm { get; set; }
}

public class LogDetalheDto
{
    public LogRequisicaoDto? Requisicao { get; set; }
    public List<LogIntegracaoDto> Integracoes { get; set; } = new();
}

public class LogIntegracaoDto
{
    public string Id { get; set; } = string.Empty;
    public string RequisicaoId { get; set; } = string.Empty;
    public string IntegracaoId { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public string? Metodo { get; set; }
    public int? StatusHttp { get; set; }
    public object? RequestHeaders { get; set; }
    public object? RequestBody { get; set; }
    public object? ResponseHeaders { get; set; }
    public object? ResponseBody { get; set; }
    public int? TempoRespostaMs { get; set; }
    public DateTime CriadoEm { get; set; }
}

public class MetricsDto
{
    public int Total { get; set; }
    public int Sucesso { get; set; }
    public int Erros { get; set; }
    public Dictionary<string, int> PorCanal { get; set; } = new();
}

public class ProcessamentoChartDto
{
    public string Timestamp { get; set; } = string.Empty;
    public int Email { get; set; }
    public int Sms { get; set; }
    public int Whatsapp { get; set; }
}

public class ConsumoChartDto
{
    public string CentroCusto { get; set; } = string.Empty;
    public int Quantidade { get; set; }
}

public class ErrorDto
{
    public string RequisicaoId { get; set; } = string.Empty;
    public string Canal { get; set; } = string.Empty;
    public string Erro { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
}

