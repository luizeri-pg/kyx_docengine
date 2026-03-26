using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using KYX.NotifyHUB.API.Models.DTOs;
using KYX.NotifyHUB.API.Models.DTOs.Auth;
using KYX.NotifyHUB.API.Services;

namespace KYX.NotifyHUB.API.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Autentica usuário e retorna JWT token
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var requisicaoId = Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation("[AUTH] Tentativa de login recebida: {Username}, RequisicaoId: {RequisicaoId}", 
                request.Username, requisicaoId);

            var resultado = await _authService.LoginAsync(request);

            stopwatch.Stop();
            _logger.LogInformation("[AUTH] Login bem-sucedido para: {Username}", request.Username);

            return Ok(ApiResponse<LoginResponse>.Success(resultado, requisicaoId, stopwatch.ElapsedMilliseconds));
        }
        catch (UnauthorizedAccessException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning("[AUTH] Erro de autenticação: {Message}", ex.Message);
            
            return Unauthorized(ApiResponse<LoginResponse>.Error(ex.Message, requisicaoId, stopwatch.ElapsedMilliseconds));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[AUTH] Erro inesperado no login");
            
            return StatusCode(500, ApiResponse<LoginResponse>.Error(
                "Erro interno ao processar login", requisicaoId, stopwatch.ElapsedMilliseconds));
        }
    }
}

