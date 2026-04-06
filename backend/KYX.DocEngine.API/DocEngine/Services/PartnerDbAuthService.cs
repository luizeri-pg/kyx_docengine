using System.Data;
using Dapper;
using KYX.DocEngine.API.Configuration;
using KYX.DocEngine.API.Models.DTOs.Auth;
using KYX.DocEngine.API.Models.Entities;
using Microsoft.Extensions.Options;

namespace KYX.DocEngine.API.Services;

/// <summary>
/// Implementação de autenticação para banco Partner/KYX usando Dapper + SQL nativo.
/// Não depende de EF Core - usa nomes de colunas reais da tabela legada (id_usuario, str_login, etc.).
/// </summary>
public class PartnerDbAuthService : IAuthService
{
    private readonly IDbConnection _dbConnection;
    private readonly IJwtService _jwtService;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<PartnerDbAuthService> _logger;

    public PartnerDbAuthService(
        IDbConnection dbConnection,
        IJwtService jwtService,
        IOptions<JwtSettings> jwtSettings,
        ILogger<PartnerDbAuthService> logger)
    {
        _dbConnection = dbConnection;
        _jwtService = jwtService;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var username = (request.Username ?? string.Empty).Trim();
        
        _logger.LogInformation("[PartnerDB] Tentativa de login para: {Username}", username);

        // Query SQL nativo com nomes de colunas legados
        const string sql = @"
            SELECT id_usuario as Id, 
                   str_login as Login, 
                   str_descricao as Nome, 
                   str_senha as Senha, 
                   bloqueado as Bloqueado,
                   email as Email
            FROM tb_usuario 
            WHERE str_login = @login
               OR email = @login
               OR str_descricao = @login
            LIMIT 1";

        var usuario = await _dbConnection.QueryFirstOrDefaultAsync<PartnerUsuario>(
            sql, 
            new { login = username });

        if (usuario == null)
        {
            _logger.LogWarning("[PartnerDB] Usuário não encontrado: {Username}", username);
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        // Verificar se está bloqueado (bloqueado = true significa inativo)
        if (usuario.Bloqueado)
        {
            _logger.LogWarning("[PartnerDB] Usuário bloqueado: {Username} (Id: {Id})", username, usuario.Id);
            throw new UnauthorizedAccessException("Usuário bloqueado");
        }

        // Verificar senha com BCrypt
        if (!BCrypt.Net.BCrypt.Verify(request.Password, usuario.Senha))
        {
            _logger.LogWarning("[PartnerDB] Senha incorreta para: {Username}", username);
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        _logger.LogInformation("[PartnerDB] Login bem-sucedido: {Username} (Id: {Id})", username, usuario.Id);

        // Gerar token JWT
        var displayName = !string.IsNullOrWhiteSpace(usuario.Nome) ? usuario.Nome : usuario.Login;
        var token = _jwtService.GenerateToken(displayName, usuario.Id);

        return new LoginResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = _jwtSettings.ExpirationMinutes * 60
        };
    }
}
