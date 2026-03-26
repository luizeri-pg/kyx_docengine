namespace KYX.DocEngine.API.Models.DTOs.Audit;

public class RequestLogEntryDto
{
    public string Id { get; set; } = null!;
    public string RequisicaoId { get; set; } = null!;
    public string Endpoint { get; set; } = null!;
    public string RequestBody { get; set; } = null!;
    public string? ResponseBody { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? UserId { get; set; }
    public string? CentroCusto { get; set; }
    public long? DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Canal { get; set; }
    public string? Erro { get; set; }
}
