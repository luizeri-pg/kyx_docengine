using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using KYX.DocEngine.API.Configuration;
using KYX.DocEngine.API.Data;
using KYX.DocEngine.API.Models.DTOs.Auth;
using KYX.DocEngine.API.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KYX.DocEngine.API.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

public partial class AuthService : IAuthService
{
    private readonly DocEngineDbContext _db;
    private readonly IJwtService _jwtService;
    private readonly JwtSettings _jwtSettings;
    private readonly AuthSettings _authSettings;
    private readonly SchemaTableOptions _schema;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        DocEngineDbContext db,
        IJwtService jwtService,
        IOptions<JwtSettings> jwtSettings,
        IOptions<AuthSettings> authSettings,
        IOptions<SchemaTableOptions> schemaOptions,
        ILogger<AuthService> logger)
    {
        _db = db;
        _jwtService = jwtService;
        _jwtSettings = jwtSettings.Value;
        _authSettings = authSettings.Value;
        _schema = schemaOptions.Value;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (_authSettings.UseDatabaseForLogin)
        {
            try
            {
                // Trim: clientes costumam enviar espaços; id "301" tem de bater com Id mapeado de id_usuario.
                var username = (request.Username ?? string.Empty).Trim();
                var loginCol = _schema.Usuario.Login;
                // Só comparar Id com username se for número inteiro — senão o conversor string↔int rebenta (ex.: str_login "98765432100.0001").
                var matchByPk = _schema.Usuario.IdIntegerType &&
                    int.TryParse(username, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

                Usuario? usuario;
                if (!string.IsNullOrWhiteSpace(loginCol))
                {
                    usuario = matchByPk
                        ? await _db.Usuarios.AsNoTracking()
                            .FirstOrDefaultAsync(
                                u => u.Ativo &&
                                     (u.Id == username ||
                                      u.Email == username ||
                                      u.Nome == username ||
                                      u.Login == username),
                                cancellationToken)
                        : await _db.Usuarios.AsNoTracking()
                            .FirstOrDefaultAsync(
                                u => u.Ativo &&
                                     (u.Email == username ||
                                      u.Nome == username ||
                                      u.Login == username),
                                cancellationToken);
                }
                else
                {
                    usuario = matchByPk
                        ? await _db.Usuarios.AsNoTracking()
                            .FirstOrDefaultAsync(
                                u => u.Ativo &&
                                     (u.Id == username ||
                                      u.Email == username ||
                                      u.Nome == username),
                                cancellationToken)
                        : await _db.Usuarios.AsNoTracking()
                            .FirstOrDefaultAsync(
                                u => u.Ativo &&
                                     (u.Email == username || u.Nome == username),
                                cancellationToken);
                }

                if (usuario != null)
                {
                    if (!PasswordMatches(request.Password, usuario.Senha))
                    {
                        throw new UnauthorizedAccessException("Credenciais inválidas");
                    }

                    _logger.LogInformation("Login autenticado via tb_usuario: {Id}", usuario.Id);
                    var displayName = !string.IsNullOrWhiteSpace(usuario.Nome)
                        ? usuario.Nome
                        : !string.IsNullOrWhiteSpace(usuario.Email)
                            ? usuario.Email
                            : usuario.Login ?? username;
                    return BuildResponse(displayName ?? username, usuario.Id);
                }

                // Utilizador não encontrado na BD
                if (!_authSettings.UseAllowedLoginsFallback)
                {
                    throw new UnauthorizedAccessException("Credenciais inválidas");
                }

                _logger.LogDebug("Utilizador {Username} não encontrado em tb_usuario; a tentar fallback em config.", request.Username);
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (!_authSettings.UseAllowedLoginsFallback)
                {
                    _logger.LogError(ex, "Falha ao consultar tb_usuario; autenticação só por BD está ativa.");
                    throw new UnauthorizedAccessException("Serviço de autenticação indisponível.");
                }

                _logger.LogWarning(ex,
                    "Não foi possível autenticar em tb_usuario (BD indisponível ou schema). A tentar Auth:AllowedLogins.");
            }
        }

        // Lista em appsettings: só quando a BD não é a única fonte (UseDatabaseForLogin=false) ou fallback explícito.
        var useConfigList = !_authSettings.UseDatabaseForLogin || _authSettings.UseAllowedLoginsFallback;
        if (!useConfigList)
        {
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        var allowed = _authSettings.AllowedLogins ?? new List<AuthLoginPair>();
        var ok = allowed.Any(p =>
            string.Equals(p.Username, request.Username, StringComparison.Ordinal)
            && p.Password == request.Password);

        if (!ok)
        {
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        _logger.LogInformation("Login autenticado via Auth:AllowedLogins (config): {Username}", request.Username);
        return BuildResponse(request.Username ?? string.Empty, Guid.NewGuid().ToString());
    }

    private LoginResponse BuildResponse(string username, string userId) => new()
    {
        ExpiresIn = _jwtSettings.ExpirationMinutes * 60,
        AccessToken = _jwtService.GenerateToken(username, userId),
        TokenType = "Bearer"
    };

    /// <summary>
    /// BCrypt (preferencial) ou MD5 hex UTF-8 quando <see cref="AuthSettings.PasswordVerification"/> = BcryptOrMd5Hex.
    /// </summary>
    private bool PasswordMatches(string plainPassword, string? storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        var mode = (_authSettings.PasswordVerification ?? "BcryptOrMd5Hex").Trim();
        var bcryptOnly = string.Equals(mode, "BcryptOnly", StringComparison.OrdinalIgnoreCase);

        if (LooksLikeBcryptHash(storedHash))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(plainPassword, storedHash);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "BCrypt.Verify falhou para hash armazenado.");
                return false;
            }
        }

        if (bcryptOnly)
        {
            return false;
        }

        if (string.Equals(mode, "BcryptOrMd5Hex", StringComparison.OrdinalIgnoreCase) &&
            Md5HexRegex().IsMatch(storedHash.Trim()))
        {
            return VerifyMd5Utf8Hex(plainPassword, storedHash.Trim());
        }

        return false;
    }

    private static bool LooksLikeBcryptHash(string storedHash) =>
        storedHash.StartsWith("$2a$", StringComparison.Ordinal) ||
        storedHash.StartsWith("$2b$", StringComparison.Ordinal) ||
        storedHash.StartsWith("$2y$", StringComparison.Ordinal);

    private static bool VerifyMd5Utf8Hex(string password, string storedHex32)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(password));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return string.Equals(hex, storedHex32, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("^[0-9a-fA-F]{32}$", RegexOptions.Compiled)]
    private static partial Regex Md5HexRegex();
}
