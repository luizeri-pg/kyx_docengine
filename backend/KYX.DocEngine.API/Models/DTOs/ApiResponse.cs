namespace KYX.NotifyHUB.API.Models.DTOs;

/// <summary>
/// Resposta padrão da API
/// </summary>
public class ApiResponse<T>
{
    public bool Sucesso { get; set; }
    public string? Mensagem { get; set; }
    public long TempoProcessamento { get; set; }
    public string? RequisicaoId { get; set; }
    public T? Resultado { get; set; }

    public static ApiResponse<T> Success(T? resultado, string? requisicaoId = null, long tempoProcessamento = 0)
    {
        return new ApiResponse<T>
        {
            Sucesso = true,
            Mensagem = null,
            TempoProcessamento = tempoProcessamento,
            RequisicaoId = requisicaoId,
            Resultado = resultado
        };
    }

    public static ApiResponse<T> SuccessWithMessage(T? resultado, string mensagem, string? requisicaoId = null, long tempoProcessamento = 0)
    {
        return new ApiResponse<T>
        {
            Sucesso = true,
            Mensagem = mensagem,
            TempoProcessamento = tempoProcessamento,
            RequisicaoId = requisicaoId,
            Resultado = resultado
        };
    }

    public static ApiResponse<T> Error(string mensagem, string? requisicaoId = null, long tempoProcessamento = 0)
    {
        return new ApiResponse<T>
        {
            Sucesso = false,
            Mensagem = mensagem,
            TempoProcessamento = tempoProcessamento,
            RequisicaoId = requisicaoId,
            Resultado = default
        };
    }
}
