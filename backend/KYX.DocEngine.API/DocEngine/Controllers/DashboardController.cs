using System.Diagnostics;
using KYX.DocEngine.API.Configuration;
using KYX.DocEngine.API.Data;
using KYX.DocEngine.API.Helpers;
using KYX.DocEngine.API.Models.DTOs;
using KYX.DocEngine.API.Models.DTOs.Dashboard;
using KYX.DocEngine.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KYX.DocEngine.API.Controllers;

/// <summary>
/// Histórico em <c>tb_log_requisicao</c> / <c>tb_log_integracao</c> (Notify + DocEngine).
/// O front usa <c>/dashboard/history</c> e <c>/dashboard/logs/…</c>.
/// </summary>
[ApiController]
[Authorize]
[Route("dashboard")]
public class DashboardController : ControllerBase
{
    private readonly DocEngineDbContext _db;
    private readonly SchemaTableOptions _schema;

    public DashboardController(DocEngineDbContext db, IOptions<SchemaTableOptions> schemaOptions)
    {
        _db = db;
        _schema = schemaOptions.Value;
    }

    private bool MapsLogCanal => !string.IsNullOrWhiteSpace(_schema.LogRequisicao.Canal);

    /// <summary>Últimos registos de auditoria (uma linha por requisição na BD, conforme índice único).</summary>
    [HttpGet("history")]
    public async Task<IActionResult> History(
        [FromQuery] string? canal,
        [FromQuery] string? centroCusto,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var rid = Guid.NewGuid().ToString();
        limit = Math.Clamp(limit, 1, 500);
        offset = Math.Max(0, offset);

        try
        {
            IQueryable<LogRequisicao> q = _db.LogRequisicoes.AsNoTracking();
            if (MapsLogCanal && !string.IsNullOrWhiteSpace(canal))
                q = q.Where(l => l.Canal == canal);
            if (!string.IsNullOrWhiteSpace(centroCusto))
                q = q.Where(l => l.CentroCusto == centroCusto);

            var rows = await q
                .OrderByDescending(l => l.CriadoEm)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(cancellationToken);

            var resultado = rows.Select(MapLogRow).ToList();

            return Ok(new ApiResponse<IReadOnlyList<LogRequisicaoListItemDto>>
            {
                Sucesso = true,
                RequisicaoId = rid,
                TempoProcessamento = sw.ElapsedMilliseconds,
                Resultado = resultado
            });
        }
        catch (Exception ex) when (PostgresErrors.IsUndefinedTable(ex))
        {
            return Ok(new ApiResponse<IReadOnlyList<LogRequisicaoListItemDto>>
            {
                Sucesso = true,
                RequisicaoId = rid,
                TempoProcessamento = sw.ElapsedMilliseconds,
                Resultado = Array.Empty<LogRequisicaoListItemDto>()
            });
        }
    }

    [HttpGet("logs/{requisicaoId}")]
    public async Task<IActionResult> Detalhe(string requisicaoId, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var rid = Guid.NewGuid().ToString();

        if (string.IsNullOrWhiteSpace(requisicaoId))
        {
            return BadRequest(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = "Informe o Request ID.",
                RequisicaoId = rid,
                TempoProcessamento = sw.ElapsedMilliseconds
            });
        }

        try
        {
            var log = await _db.LogRequisicoes.AsNoTracking()
                .FirstOrDefaultAsync(l => l.RequisicaoId == requisicaoId, cancellationToken);

            if (log is null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Sucesso = false,
                    Mensagem = "Requisição não encontrada.",
                    RequisicaoId = rid,
                    TempoProcessamento = sw.ElapsedMilliseconds
                });
            }

            IReadOnlyList<LogIntegracaoItemDto> integracoes;
            try
            {
                var ints = await _db.LogIntegracoes.AsNoTracking()
                    .Where(i => i.RequisicaoId == requisicaoId)
                    .OrderBy(i => i.CriadoEm)
                    .ToListAsync(cancellationToken);
                integracoes = ints.Select(MapIntegracaoRow).ToList();
            }
            catch (Exception ex) when (PostgresErrors.IsUndefinedTable(ex))
            {
                integracoes = Array.Empty<LogIntegracaoItemDto>();
            }

            return Ok(new ApiResponse<LogDetalheDto>
            {
                Sucesso = true,
                RequisicaoId = rid,
                TempoProcessamento = sw.ElapsedMilliseconds,
                Resultado = new LogDetalheDto
                {
                    Requisicao = MapLogRow(log),
                    Integracoes = integracoes
                }
            });
        }
        catch (Exception ex) when (PostgresErrors.IsUndefinedTable(ex))
        {
            return NotFound(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = "Tabela de log indisponível.",
                RequisicaoId = rid,
                TempoProcessamento = sw.ElapsedMilliseconds
            });
        }
    }

    private LogRequisicaoListItemDto MapLogRow(LogRequisicao r) =>
        new()
        {
            Id = r.Id,
            RequisicaoId = r.RequisicaoId,
            UsuarioId = r.UsuarioId,
            Canal = MapsLogCanal ? r.Canal : null,
            CentroCusto = r.CentroCusto,
            StatusHttp = r.StatusHttp,
            TempoRespostaMs = r.TempoRespostaMs,
            Erro = r.Erro,
            CriadoEm = r.CriadoEm
        };

    private static LogIntegracaoItemDto MapIntegracaoRow(LogIntegracao i) =>
        new()
        {
            Id = i.Id,
            RequisicaoId = i.RequisicaoId,
            IntegracaoId = i.IntegracaoId,
            Endpoint = i.Endpoint,
            Metodo = i.Metodo,
            StatusHttp = i.StatusHttp,
            TempoRespostaMs = i.TempoRespostaMs,
            CriadoEm = i.CriadoEm
        };
}
