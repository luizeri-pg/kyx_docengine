using System.Diagnostics;
using KYX.DocEngine.API.Models.DTOs;
using KYX.DocEngine.API.Models.DTOs.Auth;
using KYX.DocEngine.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace KYX.DocEngine.API.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest? request)
    {
        var stopwatch = Stopwatch.StartNew();
        var requisicaoId = Guid.NewGuid().ToString();
        try
        {
            if (request is null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Sucesso = false,
                    Mensagem = "Body inválido ou ausente.",
                    RequisicaoId = requisicaoId,
                    TempoProcessamento = stopwatch.ElapsedMilliseconds
                });
            }

            var result = await _authService.LoginAsync(request);
            return Ok(new ApiResponse<LoginResponse>
            {
                Sucesso = true,
                RequisicaoId = requisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds,
                Resultado = result
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = ex.Message,
                RequisicaoId = requisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = $"Erro ao autenticar: {ex.Message}",
                RequisicaoId = requisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds
            });
        }
    }
}
