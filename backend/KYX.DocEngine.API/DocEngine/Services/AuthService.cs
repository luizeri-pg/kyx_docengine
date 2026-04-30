using KYX.DocEngine.API.Configuration;
using KYX.DocEngine.API.Models.DTOs.Auth;
using Microsoft.Extensions.Options;

namespace KYX.DocEngine.API.Services;

/// <summary>
/// Interface de autenticação - implementações: AuthService (orquestrador), PartnerDbAuthService, FallbackAuthService.
/// </summary>
public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Serviço orquestrador de autenticação.
/// Delega para implementações específicas com base em Auth:Mode:
/// - PartnerDB → PartnerDbAuthService (Dapper + SQL nativo)
/// - FallbackOnly → FallbackAuthService (em memória)
/// - StandardDB → (não implementado, lança NotImplementedException)
/// </summary>
public class AuthService : IAuthService
{
    private readonly AuthSettings _authSettings;
    private readonly PartnerDbAuthService _partnerDbService;
    private readonly FallbackAuthService _fallbackService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IOptions<AuthSettings> authSettings,
        PartnerDbAuthService partnerDbService,
        FallbackAuthService fallbackService,
        ILogger<AuthService> logger)
    {
        _authSettings = authSettings.Value;
        _partnerDbService = partnerDbService;
        _fallbackService = fallbackService;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var username = (request.Username ?? string.Empty).Trim();
        var mode = _authSettings.Mode ?? "PartnerDB";

        _logger.LogInformation("[AuthService] Tentativa de login: {Username}, Mode: {Mode}", username, mode);

        // Modo PartnerDB - usa Dapper + SQL nativo para tb_usuario legada
        if (_authSettings.IsPartnerDbMode)
        {
            try
            {
                return await _partnerDbService.LoginAsync(request, cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                // Partner rejeitou (credenciais, bloqueado, etc.): se o login existir em AllowedLogins,
                // tentar fallback (útil quando tb_usuario tem o mesmo str_login com hash desatualizado).
                if (_authSettings.FallbackEnabled
                    && _authSettings.AllowedLogins?.Count > 0
                    && _authSettings.AllowedLogins.Any(p =>
                        string.Equals((p.Username ?? string.Empty).Trim(), username, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogInformation(
                        "[AuthService] PartnerDB não autenticou; a tentar fallback em memória para {Username}",
                        username);
                    return await _fallbackService.LoginAsync(request, cancellationToken);
                }

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AuthService] Falha no PartnerDB para {Username}", username);

                // Fallback habilitado?
                if (_authSettings.FallbackEnabled && _authSettings.AllowedLogins?.Count > 0)
                {
                    _logger.LogInformation("[AuthService] Tentando fallback em memória para {Username}", username);
                    return await _fallbackService.LoginAsync(request, cancellationToken);
                }

                // Sem fallback - relança como indisponível
                throw new UnauthorizedAccessException("Serviço de autenticação indisponível.");
            }
        }

        // Modo FallbackOnly - só login em memória
        if (_authSettings.IsFallbackOnlyMode)
        {
            return await _fallbackService.LoginAsync(request, cancellationToken);
        }

        // Modo StandardDB - EF Core padrão (não implementado nesta versão)
        if (_authSettings.IsStandardDbMode)
        {
            _logger.LogError("[AuthService] Mode=StandardDB não implementado. Use PartnerDB ou FallbackOnly.");
            throw new NotImplementedException("Modo StandardDB requer implementação separada com EF Core padrão");
        }

        // Modo desconhecido
        _logger.LogError("[AuthService] Modo desconhecido: {Mode}", mode);
        throw new InvalidOperationException($"Modo de autenticação inválido: {mode}");
    }
}
