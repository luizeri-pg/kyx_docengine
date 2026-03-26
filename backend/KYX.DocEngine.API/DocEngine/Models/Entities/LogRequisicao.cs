namespace KYX.DocEngine.API.Models.Entities;

/// <summary>
/// Alinhado ao padrão NotifyHUB: tabela <c>tb_log_requisicao</c> (mesmo nome em todos os ambientes).
/// DocEngine usa <see cref="Canal"/> = <c>docengine</c> e grava endpoint/corpos em JSON em <c>request_payload</c>/<c>response_payload</c>.
/// </summary>
public class LogRequisicao
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string RequisicaoId { get; set; } = null!;

    public string? UsuarioId { get; set; }

    /// <summary>Ex.: docengine (PDF/API), email, sms — legado Notify.</summary>
    public string Canal { get; set; } = "docengine";

    public string? CentroCusto { get; set; }

    /// <summary>JSON (jsonb): endpoint, method, body, etc.</summary>
    public string? RequestPayload { get; set; }

    /// <summary>JSON (jsonb): resposta ou objeto com raw.</summary>
    public string? ResponsePayload { get; set; }

    public int? StatusHttp { get; set; }

    public int? TempoRespostaMs { get; set; }

    public string? Erro { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public virtual ICollection<LogIntegracao> LogIntegracoes { get; set; } = new List<LogIntegracao>();
    public virtual ICollection<Consumo> Consumos { get; set; } = new List<Consumo>();
}
