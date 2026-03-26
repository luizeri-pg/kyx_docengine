using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KYX.NotifyHUB.API.Configuration;
using KYX.NotifyHUB.API.Data;
using KYX.NotifyHUB.API.Models.DTOs;
using KYX.NotifyHUB.API.Models.DTOs.Dashboard;
using KYX.NotifyHUB.API.Stores;
using Microsoft.Extensions.Options;

namespace KYX.NotifyHUB.API.Controllers;

[ApiController]
[Route("dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly MockStore _mockStore;
    private readonly AppSettings _settings;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        AppDbContext context,
        MockStore mockStore,
        IOptions<AppSettings> settings,
        ILogger<DashboardController> logger)
    {
        _context = context;
        _mockStore = mockStore;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Retorna histórico de notificações
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<List<LogRequisicaoDto>>>> GetHistory(
        [FromQuery] string? canal,
        [FromQuery] string? centroCusto,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        try
        {
            List<LogRequisicaoDto> resultado;

            if (_settings.UseMocks)
            {
                var logs = _mockStore.ListLogRequisicoes(canal, centroCusto, limit, offset);
                resultado = logs.Select(MapToDto).ToList();
            }
            else
            {
                var query = _context.LogRequisicoes.AsQueryable();
                
                if (!string.IsNullOrEmpty(canal))
                    query = query.Where(l => l.Canal == canal);
                if (!string.IsNullOrEmpty(centroCusto))
                    query = query.Where(l => l.CentroCusto == centroCusto);

                var logs = await query
                    .OrderByDescending(l => l.CriadoEm)
                    .Skip(offset)
                    .Take(limit)
                    .ToListAsync();

                resultado = logs.Select(MapToDto).ToList();
            }

            return Ok(ApiResponse<List<LogRequisicaoDto>>.Success(resultado));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar histórico");
            return StatusCode(500, ApiResponse<List<LogRequisicaoDto>>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Retorna logs detalhados de uma requisição
    /// </summary>
    [HttpGet("logs/{requisicaoId}")]
    public async Task<ActionResult<ApiResponse<LogDetalheDto>>> GetLogs(string requisicaoId)
    {
        try
        {
            LogDetalheDto resultado;

            if (_settings.UseMocks)
            {
                var requisicao = _mockStore.GetLogRequisicao(requisicaoId);
                if (requisicao == null)
                {
                    return NotFound(ApiResponse<LogDetalheDto>.Error("Requisição não encontrada", requisicaoId));
                }

                var integracoes = _mockStore.GetLogIntegracoesByRequisicao(requisicaoId);
                resultado = new LogDetalheDto
                {
                    Requisicao = MapToDto(requisicao),
                    Integracoes = integracoes.Select(MapIntegracaoToDto).ToList()
                };
            }
            else
            {
                var requisicao = await _context.LogRequisicoes
                    .FirstOrDefaultAsync(l => l.RequisicaoId == requisicaoId);

                if (requisicao == null)
                {
                    return NotFound(ApiResponse<LogDetalheDto>.Error("Requisição não encontrada", requisicaoId));
                }

                var integracoes = await _context.LogIntegracoes
                    .Where(l => l.RequisicaoId == requisicaoId)
                    .Include(l => l.Integracao)
                    .ToListAsync();

                resultado = new LogDetalheDto
                {
                    Requisicao = MapToDto(requisicao),
                    Integracoes = integracoes.Select(MapIntegracaoToDto).ToList()
                };
            }

            return Ok(ApiResponse<LogDetalheDto>.Success(resultado, requisicaoId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar logs da requisição {RequisicaoId}", requisicaoId);
            return StatusCode(500, ApiResponse<LogDetalheDto>.Error(ex.Message, requisicaoId));
        }
    }

    /// <summary>
    /// Retorna métricas do dashboard
    /// </summary>
    [HttpGet("metrics")]
    public async Task<ActionResult<ApiResponse<MetricsDto>>> GetMetrics()
    {
        try
        {
            MetricsDto resultado;

            if (_settings.UseMocks)
            {
                var (total, sucesso, erros, porCanal) = _mockStore.GetStats();
                resultado = new MetricsDto
                {
                    Total = total,
                    Sucesso = sucesso,
                    Erros = erros,
                    PorCanal = porCanal
                };
            }
            else
            {
                var total = await _context.LogRequisicoes.CountAsync();
                var sucesso = await _context.LogRequisicoes.CountAsync(l => l.StatusHttp == 200);
                var erros = await _context.LogRequisicoes.CountAsync(l => 
                    l.Erro != null || (l.StatusHttp != null && l.StatusHttp >= 400));

                var porCanalRaw = await _context.LogRequisicoes
                    .GroupBy(l => l.Canal)
                    .Select(g => new { Canal = g.Key, Count = g.Count() })
                    .ToListAsync();

                resultado = new MetricsDto
                {
                    Total = total,
                    Sucesso = sucesso,
                    Erros = erros,
                    PorCanal = porCanalRaw.ToDictionary(x => x.Canal, x => x.Count)
                };
            }

            return Ok(ApiResponse<MetricsDto>.Success(resultado));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar métricas");
            return StatusCode(500, ApiResponse<MetricsDto>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Retorna dados para gráfico de processamentos por hora
    /// </summary>
    [HttpGet("chart/processamentos")]
    public async Task<ActionResult<ApiResponse<List<ProcessamentoChartDto>>>> GetChartProcessamentos()
    {
        try
        {
            var logs = _settings.UseMocks
                ? _mockStore.ListLogRequisicoes(limit: 100)
                : await _context.LogRequisicoes.OrderByDescending(l => l.CriadoEm).Take(100).ToListAsync();

            var porHora = logs
                .GroupBy(l => l.CriadoEm.ToString("yyyy-MM-ddTHH:00:00"))
                .Select(g => new ProcessamentoChartDto
                {
                    Timestamp = g.Key,
                    Email = g.Count(l => l.Canal == "email"),
                    Sms = g.Count(l => l.Canal == "sms"),
                    Whatsapp = g.Count(l => l.Canal == "whatsapp")
                })
                .ToList();

            return Ok(ApiResponse<List<ProcessamentoChartDto>>.Success(porHora));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar dados do gráfico");
            return StatusCode(500, ApiResponse<List<ProcessamentoChartDto>>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Retorna dados para gráfico de consumo por centro de custo
    /// </summary>
    [HttpGet("chart/consumo")]
    public async Task<ActionResult<ApiResponse<List<ConsumoChartDto>>>> GetChartConsumo()
    {
        try
        {
            var logs = _settings.UseMocks
                ? _mockStore.ListLogRequisicoes(limit: 100)
                : await _context.LogRequisicoes.OrderByDescending(l => l.CriadoEm).Take(100).ToListAsync();

            var porCentroCusto = logs
                .GroupBy(l => l.CentroCusto ?? "Sem centro de custo")
                .Select(g => new ConsumoChartDto
                {
                    CentroCusto = g.Key,
                    Quantidade = g.Count()
                })
                .ToList();

            return Ok(ApiResponse<List<ConsumoChartDto>>.Success(porCentroCusto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar dados de consumo");
            return StatusCode(500, ApiResponse<List<ConsumoChartDto>>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Retorna erros recentes
    /// </summary>
    [HttpGet("errors")]
    public async Task<ActionResult<ApiResponse<List<ErrorDto>>>> GetErrors()
    {
        try
        {
            List<ErrorDto> erros;

            if (_settings.UseMocks)
            {
                var logs = _mockStore.ListLogRequisicoes(limit: 50);
                erros = logs
                    .Where(l => !string.IsNullOrEmpty(l.Erro) || (l.StatusHttp.HasValue && l.StatusHttp >= 400))
                    .Take(10)
                    .Select(l => new ErrorDto
                    {
                        RequisicaoId = l.RequisicaoId,
                        Canal = l.Canal,
                        Erro = l.Erro ?? $"Erro HTTP {l.StatusHttp}",
                        CriadoEm = l.CriadoEm
                    })
                    .ToList();
            }
            else
            {
                var logs = await _context.LogRequisicoes
                    .Where(l => l.Erro != null || (l.StatusHttp != null && l.StatusHttp >= 400))
                    .OrderByDescending(l => l.CriadoEm)
                    .Take(10)
                    .ToListAsync();

                erros = logs.Select(l => new ErrorDto
                {
                    RequisicaoId = l.RequisicaoId,
                    Canal = l.Canal,
                    Erro = l.Erro ?? $"Erro HTTP {l.StatusHttp}",
                    CriadoEm = l.CriadoEm
                }).ToList();
            }

            return Ok(ApiResponse<List<ErrorDto>>.Success(erros));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar erros");
            return StatusCode(500, ApiResponse<List<ErrorDto>>.Error(ex.Message));
        }
    }

    private static LogRequisicaoDto MapToDto(Models.Entities.LogRequisicao log) => new()
    {
        Id = log.Id,
        RequisicaoId = log.RequisicaoId,
        UsuarioId = log.UsuarioId,
        Canal = log.Canal,
        CentroCusto = log.CentroCusto,
        RequestPayload = string.IsNullOrEmpty(log.RequestPayload) ? null : JsonSerializer.Deserialize<object>(log.RequestPayload),
        ResponsePayload = string.IsNullOrEmpty(log.ResponsePayload) ? null : JsonSerializer.Deserialize<object>(log.ResponsePayload),
        StatusHttp = log.StatusHttp,
        TempoRespostaMs = log.TempoRespostaMs,
        Erro = log.Erro,
        CriadoEm = log.CriadoEm
    };

    private static LogIntegracaoDto MapIntegracaoToDto(Models.Entities.LogIntegracao log) => new()
    {
        Id = log.Id,
        RequisicaoId = log.RequisicaoId,
        IntegracaoId = log.IntegracaoId,
        Endpoint = log.Endpoint,
        Metodo = log.Metodo,
        StatusHttp = log.StatusHttp,
        RequestHeaders = string.IsNullOrEmpty(log.RequestHeaders) ? null : JsonSerializer.Deserialize<object>(log.RequestHeaders),
        RequestBody = string.IsNullOrEmpty(log.RequestBody) ? null : JsonSerializer.Deserialize<object>(log.RequestBody),
        ResponseHeaders = string.IsNullOrEmpty(log.ResponseHeaders) ? null : JsonSerializer.Deserialize<object>(log.ResponseHeaders),
        ResponseBody = string.IsNullOrEmpty(log.ResponseBody) ? null : JsonSerializer.Deserialize<object>(log.ResponseBody),
        TempoRespostaMs = log.TempoRespostaMs,
        CriadoEm = log.CriadoEm
    };
}

