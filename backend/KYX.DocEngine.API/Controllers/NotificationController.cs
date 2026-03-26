using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KYX.NotifyHUB.API.Models.DTOs;
using KYX.NotifyHUB.API.Models.DTOs.Notification;
using KYX.NotifyHUB.API.Services;

namespace KYX.NotifyHUB.API.Controllers;

[ApiController]
[Route("notification")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(INotificationService notificationService, ILogger<NotificationController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Envia uma notificação (email, SMS ou WhatsApp)
    /// </summary>
    [HttpPost("send")]
    public async Task<ActionResult<ApiResponse<NotificationSendResponse>>> Send([FromBody] NotificationSendRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var requisicaoId = request.RequisicaoId ?? Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation("[NOTIFICATION] Recebida requisição de notificação - Canal: {Canal}, RequisicaoId: {RequisicaoId}",
                request.Config.Canal, requisicaoId);

            var resultado = await _notificationService.SendNotificationAsync(request, requisicaoId);

            stopwatch.Stop();
            return Ok(ApiResponse<NotificationSendResponse>.Success(resultado, requisicaoId, stopwatch.ElapsedMilliseconds));
        }
        catch (ArgumentException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning("[NOTIFICATION] Erro de validação: {Message}", ex.Message);
            
            return BadRequest(ApiResponse<NotificationSendResponse>.Error(ex.Message, requisicaoId, stopwatch.ElapsedMilliseconds));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[NOTIFICATION] Erro ao processar notificação {RequisicaoId}", requisicaoId);
            
            return StatusCode(500, ApiResponse<NotificationSendResponse>.Error(
                ex.Message ?? "Erro ao processar notificação", requisicaoId, stopwatch.ElapsedMilliseconds));
        }
    }
}

