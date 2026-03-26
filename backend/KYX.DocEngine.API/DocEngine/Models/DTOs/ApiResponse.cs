namespace KYX.DocEngine.API.Models.DTOs;

public class ApiResponse<T>
{
    public bool Sucesso { get; set; }
    public string? Mensagem { get; set; }
    public long TempoProcessamento { get; set; }
    public string RequisicaoId { get; set; } = null!;
    public T? Resultado { get; set; }
}
