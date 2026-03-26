using System.Diagnostics;
using KYX.DocEngine.API.Data;
using KYX.DocEngine.API.Helpers;
using KYX.DocEngine.API.Models.DTOs;
using KYX.DocEngine.API.Models.DTOs.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KYX.DocEngine.API.Controllers;

/// <summary>Auditoria por Request ID (tabela <c>tb_log_requisicao</c>, padrão NotifyHUB/DocEngine).</summary>
[ApiController]
[Authorize]
[Route("audit")]
public class AuditController : ControllerBase
{
    private readonly DocEngineDbContext _db;

    public AuditController(DocEngineDbContext db)
    {
        _db = db;
    }

    [HttpGet("logs/{requisicaoId}")]
    public async Task<IActionResult> GetLogsByRequisicao(string requisicaoId)
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

        var rows = await _db.LogRequisicoes
            .AsNoTracking()
            .Where(r => r.RequisicaoId == requisicaoId)
            .OrderByDescending(r => r.CriadoEm)
            .ToListAsync();

        var resultado = rows.Select(r =>
        {
            var (endpoint, body) = AuditPayloadHelper.ParseRequestPayload(r.RequestPayload);
            return new RequestLogEntryDto
            {
                Id = r.Id,
                RequisicaoId = r.RequisicaoId,
                Endpoint = endpoint,
                RequestBody = body,
                ResponseBody = AuditPayloadHelper.FormatResponsePayload(r.ResponsePayload),
                HttpStatusCode = r.StatusHttp,
                UserId = r.UsuarioId,
                CentroCusto = r.CentroCusto,
                DurationMs = r.TempoRespostaMs,
                CreatedAt = r.CriadoEm,
                Canal = r.Canal,
                Erro = r.Erro
            };
        }).ToList();

        return Ok(new ApiResponse<IReadOnlyList<RequestLogEntryDto>>
        {
            Sucesso = true,
            RequisicaoId = rid,
            TempoProcessamento = sw.ElapsedMilliseconds,
            Resultado = resultado
        });
    }
}
