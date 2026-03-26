using System.Text.Json;
using KYX.NotifyHUB.API.Configuration;
using KYX.NotifyHUB.API.Data;
using KYX.NotifyHUB.API.Models.DTOs.Notification;
using KYX.NotifyHUB.API.Models.Entities;
using KYX.NotifyHUB.API.Stores;
using Microsoft.Extensions.Options;

namespace KYX.NotifyHUB.API.Services;

public interface INotificationService
{
    Task<NotificationSendResponse> SendNotificationAsync(NotificationSendRequest request, string requisicaoId);
}

public class NotificationService : INotificationService
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly MockStore _mockStore;
    private readonly AppSettings _settings;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        AppDbContext context,
        IEmailService emailService,
        MockStore mockStore,
        IOptions<AppSettings> settings,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _emailService = emailService;
        _mockStore = mockStore;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<NotificationSendResponse> SendNotificationAsync(NotificationSendRequest request, string requisicaoId)
    {
        var startTime = DateTime.UtcNow;
        
        _logger.LogInformation("Processando notificação {RequisicaoId} - Canal: {Canal}, Mock: {UseMocks}", 
            requisicaoId, request.Config.Canal, _settings.UseMocks);

        // Cria log inicial
        var logRequisicao = new LogRequisicao
        {
            RequisicaoId = requisicaoId,
            Canal = request.Config.Canal,
            CentroCusto = request.Config.CentroCusto,
            RequestPayload = JsonSerializer.Serialize(request),
            StatusHttp = 200,
            TempoRespostaMs = 0
        };

        if (_settings.UseMocks)
        {
            _mockStore.CreateLogRequisicao(logRequisicao);
        }
        else
        {
            _context.LogRequisicoes.Add(logRequisicao);
            await _context.SaveChangesAsync();
        }

        try
        {
            // Processa baseado no canal
            var resultado = request.Config.Canal switch
            {
                "email" => await ProcessEmailAsync(request, requisicaoId),
                "sms" => await ProcessSmsAsync(request, requisicaoId),
                "whatsapp" => await ProcessWhatsAppAsync(request, requisicaoId),
                _ => throw new ArgumentException($"Canal não suportado: {request.Config.Canal}")
            };

            // Atualiza log com sucesso
            var tempoResposta = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            
            if (_settings.UseMocks)
            {
                _mockStore.UpdateLogRequisicao(requisicaoId, log =>
                {
                    log.StatusHttp = 200;
                    log.ResponsePayload = JsonSerializer.Serialize(resultado);
                    log.TempoRespostaMs = tempoResposta;
                });
            }
            else
            {
                logRequisicao.StatusHttp = 200;
                logRequisicao.ResponsePayload = JsonSerializer.Serialize(resultado);
                logRequisicao.TempoRespostaMs = tempoResposta;
                await _context.SaveChangesAsync();
            }

            return new NotificationSendResponse
            {
                Canal = request.Config.Canal,
                Mensagem = $"A notificação foi recebida e está em processamento{(_settings.UseMocks ? " (MOCK MODE)" : "")}."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar notificação {RequisicaoId}", requisicaoId);

            // Atualiza log com erro
            var tempoResposta = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            
            if (_settings.UseMocks)
            {
                _mockStore.UpdateLogRequisicao(requisicaoId, log =>
                {
                    log.StatusHttp = 500;
                    log.Erro = ex.Message;
                    log.TempoRespostaMs = tempoResposta;
                });
            }
            else
            {
                logRequisicao.StatusHttp = 500;
                logRequisicao.Erro = ex.Message;
                logRequisicao.TempoRespostaMs = tempoResposta;
                await _context.SaveChangesAsync();
            }

            throw;
        }
    }

    private async Task<EmailSendResult> ProcessEmailAsync(NotificationSendRequest request, string requisicaoId)
    {
        _logger.LogInformation("Processando email para {Destinatario}", request.Dados.Destinatario.Email);

        // Busca credenciais da integração se especificada
        string? credenciaisJson = null;
        if (!string.IsNullOrEmpty(request.Config.IntegracaoId))
        {
            var integracao = _settings.UseMocks 
                ? _mockStore.GetIntegracao(request.Config.IntegracaoId)
                : await _context.Integracoes.FindAsync(request.Config.IntegracaoId);

            if (integracao != null)
            {
                credenciaisJson = integracao.Credenciais;
            }
        }

        var resultado = await _emailService.SendEmailAsync(request, credenciaisJson);

        // Log de integração
        var logIntegracao = new LogIntegracao
        {
            RequisicaoId = requisicaoId,
            IntegracaoId = request.Config.IntegracaoId ?? "default-email",
            Endpoint = "send",
            Metodo = "POST",
            StatusHttp = resultado.Success ? 200 : 500,
            RequestBody = JsonSerializer.Serialize(request),
            ResponseBody = JsonSerializer.Serialize(resultado),
            TempoRespostaMs = 0
        };

        if (_settings.UseMocks)
        {
            _mockStore.CreateLogIntegracao(logIntegracao);
        }
        else
        {
            _context.LogIntegracoes.Add(logIntegracao);
            await _context.SaveChangesAsync();
        }

        if (!resultado.Success)
        {
            throw new Exception(resultado.Error ?? "Erro ao enviar email");
        }

        return resultado;
    }

    private Task<EmailSendResult> ProcessSmsAsync(NotificationSendRequest request, string requisicaoId)
    {
        _logger.LogInformation("Processando SMS para {Destinatario}", request.Dados.Destinatario.Telefone);
        
        // TODO: Implementar integração com provedor de SMS
        return Task.FromResult(new EmailSendResult
        {
            Success = true,
            MessageId = $"sms-{Guid.NewGuid()}",
            Canal = "sms",
            Destinatario = request.Dados.Destinatario.Telefone
        });
    }

    private Task<EmailSendResult> ProcessWhatsAppAsync(NotificationSendRequest request, string requisicaoId)
    {
        _logger.LogInformation("Processando WhatsApp para {Destinatario}", request.Dados.Destinatario.Telefone);
        
        // TODO: Implementar integração com WhatsApp Business API
        return Task.FromResult(new EmailSendResult
        {
            Success = true,
            MessageId = $"whatsapp-{Guid.NewGuid()}",
            Canal = "whatsapp",
            Destinatario = request.Dados.Destinatario.Telefone
        });
    }
}

