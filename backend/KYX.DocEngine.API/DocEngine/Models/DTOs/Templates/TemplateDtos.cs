using System.ComponentModel.DataAnnotations;

namespace KYX.DocEngine.API.Models.DTOs.Templates;

public class UpsertTemplateRequest
{
    [Required]
    public string Slug { get; set; } = null!;

    [Required]
    public string Name { get; set; } = null!;

    [Required]
    public string Type { get; set; } = null!;

    [Required]
    public string Content { get; set; } = null!;

    public List<string> RequiredFields { get; set; } = new();
}

public class TemplateResponse
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    /// <summary>
    /// Preenchido em GET por id / após criar ou atualizar; omitido na listagem para não trafegar HTML/PDF grande.
    /// </summary>
    public string? Content { get; set; }
    public string RequiredFields { get; set; } = "[]";
    public bool IsActive { get; set; }
}

public class InspectPdfRequest
{
    [Required]
    public string PdfBase64 { get; set; } = null!;
}
