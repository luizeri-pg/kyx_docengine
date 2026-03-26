namespace KYX.DocEngine.API.Models.DTOs.Documents;

public class DocumentJobListItemDto
{
    public Guid JobId { get; set; }
    public string RequisicaoId { get; set; } = null!;
    public string TemplateSlug { get; set; } = null!;
    public string TemplateName { get; set; } = null!;
    public string TemplateType { get; set; } = null!;
    public string CentroCusto { get; set; } = null!;
    public string NomeArquivo { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? ErrorMessage { get; set; }
    public long? ProcessingTimeMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
