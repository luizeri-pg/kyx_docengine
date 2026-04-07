using System.Security.Cryptography;
using System.Text;

namespace KYX.DocEngine.API.Helpers;

public static class JwtKeyHelper
{
    /// <summary>
    /// Retorna bytes seguros para assinatura HS256.
    /// Se a chave configurada tiver 32 bytes ou menos, deriva uma chave de 64 bytes (SHA-512).
    /// Isso evita exceções de tamanho mínimo em ambientes legados.
    /// </summary>
    public static byte[] GetSigningKeyBytes(string? secretKey)
    {
        var raw = (secretKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("Jwt:SecretKey não configurada.");

        var keyBytes = Encoding.UTF8.GetBytes(raw);
        return keyBytes.Length <= 32 ? SHA512.HashData(keyBytes) : keyBytes;
    }
}
