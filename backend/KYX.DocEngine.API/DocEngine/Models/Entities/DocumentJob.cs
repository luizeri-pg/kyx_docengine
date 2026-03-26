namespace KYX.DocEngine.API.Models.Entities;

public class DocumentJob
{
    public Guid Id { get; set; }
    public string RequisicaoId { get; set; } = null!;
    /// <summary>FK para <c>templates</c> quando a geração usa slug registado. Nulo se <see cref="TemplateSnapshotJson"/> estiver preenchido (template inline).</summary>
    public Guid? TemplateId { get; set; }
    public Template? Template { get; set; }
    /// <summary>Serialização JSON de <see cref="Template"/> quando o pedido envia <c>inlineTemplate</c> (não persistido na tabela <c>templates</c>).</summary>
    public string? TemplateSnapshotJson { get; set; }
    public string CentroCusto { get; set; } = null!;
    public string NomeArquivo { get; set; } = null!;
    public string InputData { get; set; } = null!;
    public string Status { get; set; } = "pending";
    public string? ResultBase64 { get; set; }
    public string? ErrorMessage { get; set; }
    public long? ProcessingTimeMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
