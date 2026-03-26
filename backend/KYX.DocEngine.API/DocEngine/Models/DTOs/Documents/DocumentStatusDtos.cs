namespace KYX.DocEngine.API.Models.DTOs.Documents;

public class DocumentStatusResponse
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = null!;
    public string? ErrorMessage { get; set; }
    public DocumentResult? Resultado { get; set; }
}

public class DocumentResult
{
    public string Base64 { get; set; } = null!;
    public string ContentType { get; set; } = "application/pdf";
    public string NomeArquivo { get; set; } = null!;
}
