using Microsoft.EntityFrameworkCore;
using KYX.NotifyHUB.API.Configuration;
using KYX.NotifyHUB.API.Data;
using KYX.NotifyHUB.API.Models.DTOs.Auth;
using KYX.NotifyHUB.API.Stores;
using Microsoft.Extensions.Options;

namespace KYX.NotifyHUB.API.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly MockStore _mockStore;
    private readonly AppSettings _settings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext context,
        IJwtService jwtService,
        MockStore mockStore,
        IOptions<AppSettings> settings,
        ILogger<AuthService> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _mockStore = mockStore;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        _logger.LogInformation("Tentativa de login para: {Username}, Mock: {UseMocks}", request.Username, _settings.UseMocks);

        if (_settings.UseMocks)
        {
            return await LoginMockAsync(request);
        }

        return await LoginDatabaseAsync(request);
    }

    private Task<LoginResponse> LoginMockAsync(LoginRequest request)
    {
        var usuario = _mockStore.GetUsuarioByUsername(request.Username);

        if (usuario == null)
        {
            _logger.LogWarning("Usuário não encontrado: {Username}", request.Username);
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        if (!usuario.Ativo)
        {
            _logger.LogWarning("Usuário inativo: {Username}", request.Username);
            throw new UnauthorizedAccessException("Usuário inativo");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, usuario.Senha))
        {
            _logger.LogWarning("Senha incorreta para: {Username}", request.Username);
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        // Monta roles
        var roles = usuario.Perfil?.PerfilRoles
            .Select(pr => pr.Role?.Nome ?? "")
            .Where(r => !string.IsNullOrEmpty(r))
            .ToList() ?? new List<string>();
        var rolesString = string.Join(",", roles);

        var token = _jwtService.GenerateToken(usuario.Email, usuario.Id, rolesString);

        _logger.LogInformation("Login bem-sucedido para: {Username}", request.Username);

        return Task.FromResult(new LoginResponse
        {
            ExpiresIn = _settings.Jwt.ExpirationMinutes * 60,
            AccessToken = token,
            TokenType = "Bearer"
        });
    }

    private async Task<LoginResponse> LoginDatabaseAsync(LoginRequest request)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Perfil)
                .ThenInclude(p => p!.PerfilRoles)
                    .ThenInclude(pr => pr.Role)
            .FirstOrDefaultAsync(u => 
                (u.Email == request.Username || u.Nome == request.Username) && 
                u.Ativo);

        if (usuario == null)
        {
            _logger.LogWarning("Usuário não encontrado: {Username}", request.Username);
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, usuario.Senha))
        {
            _logger.LogWarning("Senha incorreta para: {Username}", request.Username);
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        // Monta roles
        var roles = usuario.Perfil?.PerfilRoles
            .Select(pr => pr.Role?.Nome ?? "")
            .Where(r => !string.IsNullOrEmpty(r))
            .ToList() ?? new List<string>();
        var rolesString = string.Join(",", roles);

        var token = _jwtService.GenerateToken(usuario.Email, usuario.Id, rolesString);

        _logger.LogInformation("Login bem-sucedido para: {Username}", request.Username);

        return new LoginResponse
        {
            ExpiresIn = _settings.Jwt.ExpirationMinutes * 60,
            AccessToken = token,
            TokenType = "Bearer"
        };
    }
}

