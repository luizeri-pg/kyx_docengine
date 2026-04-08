using System.ComponentModel.DataAnnotations;

namespace KYX.DocEngine.API.Models.DTOs.Documents;

public class GenerateDocumentRequest : IValidatableObject
{
    [Required]
    public string RequisicaoId { get; set; } = null!;

    [Required]
    public DocumentConfig Config { get; set; } = null!;

    [Required]
    public Dictionary<string, string> Dados { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Config == null)
        {
            yield break;
        }

        var hasSlug = !string.IsNullOrWhiteSpace(Config.Template);
        var hasInline = Config.InlineTemplate != null;
        if (!hasSlug && !hasInline)
        {
            yield return new ValidationResult(
                "Informe config.template (slug de um template registado) ou config.inlineTemplate (HTML/AcroForm sem gravar na tabela templates).",
                [nameof(Config)]);
        }

        if (hasSlug && hasInline)
        {
            yield return new ValidationResult(
                "Use apenas config.template ou config.inlineTemplate, não ambos.",
                [nameof(Config)]);
        }
    }
}

public class DocumentConfig
{
    /// <summary>Slug do template na tabela <c>templates</c>. Obrigatório se <see cref="InlineTemplate"/> for nulo.</summary>
    public string? Template { get; set; }

    [Required]
    public string CentroCusto { get; set; } = null!;

    public string NomeArquivo { get; set; } = "documento.pdf";
    public string? GuidArquivo { get; set; }

    /// <summary>
    /// Template enviado no corpo do pedido — <strong>não</strong> é persistido na tabela <c>templates</c>.
    /// Tipos: <c>html</c> ou <c>acroform</c> (conteúdo = PDF em base64).
    /// </summary>
    public InlineTemplatePayload? InlineTemplate { get; set; }
}

public class InlineTemplatePayload
{
    [Required]
    public string Type { get; set; } = null!;

    [Required]
    public string Content { get; set; } = null!;

    public List<string> RequiredFields { get; set; } = new();
}

public class GenerateDocumentResponse
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = "queued";
}

/// <summary>
/// Geração síncrona: sem fila Hangfire e sem gravar em <c>document_jobs</c> (só útil para testar o motor PDF).
/// Requer <c>Documents:AllowSyncPdfGeneration</c> = true.
/// </summary>
public class GenerateSyncPdfRequest
{
    public string RequisicaoId { get; set; } = "";

    [Required]
    public InlineTemplatePayload InlineTemplate { get; set; } = null!;

    [Required]
    public Dictionary<string, string> Dados { get; set; } = new();

    public string NomeArquivo { get; set; } = "documento.pdf";
}
