using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace KYX.DocEngine.API.Models.DTOs.Documents;

public class GenerateDocumentRequest : IValidatableObject
{
    [Required]
    public string RequisicaoId { get; set; } = null!;

    [Required]
    public DocumentConfig Config { get; set; } = null!;

    [Required]
    public JsonElement Dados { get; set; }

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
    /// PDFs extras (cada string = PDF completo em Base64) concatenados **após** o PDF gerado pelo template,
    /// **na ordem do array** (primeiro elemento = primeiro anexo após o corpo).
    /// PDF nativo não recebe rodapé Chromium; com <c>HASH_DOSSIE</c> + <c>DOCENGINE_USE_CHROME_PAGE_FOOTER</c> nos dados,
    /// o serviço carimba hash e numeração global nas páginas dos anexos (ver <c>PdfDossieAnnexFooterStamper</c>).
    /// O mesmo vale para <c>dados.anexosPdf[]</c> no payload estruturado do dossiê, mapeado para esta lista.
    /// </summary>
    public List<string>? PdfsAnexosBase64 { get; set; }

    /// <summary>
    /// PDFs anexos com <c>ordem</c> explícita (menor valor = mais cedo no documento final).
    /// Se existir pelo menos um item com Base64 não vazio, esta lista **substitui**
    /// <see cref="PdfsAnexosBase64"/> (útil quando a API de origem envia <c>anexosPdf[]</c> com <c>ordem</c>).
    /// Entradas com o mesmo <c>ordem</c> mantêm a ordem de chegada no JSON.
    /// </summary>
    public List<PdfAnexoPayload>? PdfsAnexos { get; set; }

    /// <summary>
    /// PDF nativo (um ficheiro em Base64) fundido **no sítio** do placeholder <c>{{DOSSIE_BLOCO_INTERCALADO_HTML}}</c> no HTML
    /// (entre as duas metades do documento). Envie <c>DOSSIE_BLOCO_INTERCALADO_HTML</c> vazio nos dados para não duplicar conteúdo.
    /// Só aplicável a templates <c>html</c> que contenham o placeholder exacto.
    /// </summary>
    public string? PdfIntercaladoBase64 { get; set; }

    /// <summary>
    /// Template enviado no corpo do pedido — <strong>não</strong> é persistido na tabela <c>templates</c>.
    /// Tipos: <c>html</c> ou <c>acroform</c> (conteúdo = PDF em base64).
    /// </summary>
    public InlineTemplatePayload? InlineTemplate { get; set; }
}

/// <summary>Um PDF anexo com ordenação opcional (ex.: espelho de <c>anexosPdf[].ordem</c>).</summary>
public class PdfAnexoPayload
{
    public int? Ordem { get; set; }

    public string? Base64 { get; set; }
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
    public string? Base64 { get; set; }
    public string ContentType { get; set; } = "application/pdf";
    public string? NomeArquivo { get; set; }
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

    /// <inheritdoc cref="DocumentConfig.PdfsAnexosBase64"/>
    public List<string>? PdfsAnexosBase64 { get; set; }

    /// <inheritdoc cref="DocumentConfig.PdfsAnexos"/>
    public List<PdfAnexoPayload>? PdfsAnexos { get; set; }

    /// <inheritdoc cref="DocumentConfig.PdfIntercaladoBase64"/>
    public string? PdfIntercaladoBase64 { get; set; }
}
