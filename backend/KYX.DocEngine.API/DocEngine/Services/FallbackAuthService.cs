using KYX.DocEngine.API.Configuration;
using KYX.DocEngine.API.Models.DTOs.Auth;
using Microsoft.Extensions.Options;

namespace KYX.DocEngine.API.Services;

/// <summary>
/// Implementação de autenticação em memória (fallback).
/// Usada quando o banco está indisponível ou em modo de emergência.
/// Verifica contra lista configurada em Auth:AllowedLogins.
/// </summary>
public class FallbackAuthService : IAuthService
{
    private readonly AuthSettings _authSettings;
    private readonly IJwtService _jwtService;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<FallbackAuthService> _logger;

    public FallbackAuthService(
        IOptions<AuthSettings> authSettings,
        IJwtService jwtService,
        IOptions<JwtSettings> jwtSettings,
        ILogger<FallbackAuthService> logger)
    {
        _authSettings = authSettings.Value;
        _jwtService = jwtService;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var username = (request.Username ?? string.Empty).Trim();
        
        _logger.LogInformation("[Fallback] Tentativa de login para: {Username}", username);

        // Verificar contra lista em memória
        var allowed = _authSettings.AllowedLogins ?? new List<AuthLoginPair>();
        
        var user = allowed.FirstOrDefault(p =>
            string.Equals(p.Username, username, StringComparison.OrdinalIgnoreCase));

        if (user == null)
        {
            _logger.LogWarning("[Fallback] Usuário não encontrado na lista: {Username}", username);
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        // Verificar senha com BCrypt
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            _logger.LogWarning("[Fallback] Senha incorreta para: {Username}", username);
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        _logger.LogInformation("[Fallback] Login bem-sucedido: {Username}", username);

        // Gerar token JWT
        var token = _jwtService.GenerateToken(user.Username, Guid.NewGuid().ToString());

        return Task.FromResult(new LoginResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = _jwtSettings.ExpirationMinutes * 60
        });
    }
}
