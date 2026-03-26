using System.Diagnostics;
using System.Text.Json;
using Hangfire;
using KYX.DocEngine.API.Models.DTOs;
using KYX.DocEngine.API.Models.DTOs.Documents;
using KYX.DocEngine.API.Models.Entities;
using KYX.DocEngine.API.Services;
using KYX.DocEngine.API.Workers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KYX.DocEngine.API.Controllers;

[ApiController]
[Authorize]
[Route("documents")]
public class DocumentsController : ControllerBase
{
    private readonly ITemplateService _templateService;
    private readonly IDocumentJobService _documentJobService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IPdfEngineService _pdfEngine;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        ITemplateService templateService,
        IDocumentJobService documentJobService,
        IBackgroundJobClient backgroundJobClient,
        IPdfEngineService pdfEngine,
        IConfiguration configuration,
        ILogger<DocumentsController> logger)
    {
        _templateService = templateService;
        _documentJobService = documentJobService;
        _backgroundJobClient = backgroundJobClient;
        _pdfEngine = pdfEngine;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Gera PDF de forma **síncrona** (sem <c>document_jobs</c>, sem Hangfire). Só ativo com <c>Documents:AllowSyncPdfGeneration</c>.
    /// Útil para validar HTML→PDF sem migrações na BD.
    /// </summary>
    [HttpPost("generate-sync")]
    public async Task<IActionResult> GenerateSync([FromBody] GenerateSyncPdfRequest request)
    {
        if (!_configuration.GetValue("Documents:AllowSyncPdfGeneration", false))
        {
            return StatusCode(403, new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem =
                    "POST /documents/generate-sync está desligado. Defina Documents:AllowSyncPdfGeneration=true em appsettings (por defeito em Development).",
                RequisicaoId = request.RequisicaoId,
                TempoProcessamento = 0
            });
        }

        var stopwatch = Stopwatch.StartNew();
        var requisicaoId = string.IsNullOrWhiteSpace(request.RequisicaoId)
            ? Guid.NewGuid().ToString()
            : request.RequisicaoId;

        try
        {
            var inline = request.InlineTemplate;
            var templateEntity = new Template
            {
                Id = Guid.Empty,
                Slug = "inline",
                Name = "inline",
                Type = inline.Type,
                Content = inline.Content,
                RequiredFields = JsonSerializer.Serialize(inline.RequiredFields ?? new List<string>()),
                IsActive = true
            };

            var missingFields = _templateService.ValidateRequiredFields(templateEntity, request.Dados);
            if (missingFields.Any())
            {
                return BadRequest(new ApiResponse<object>
                {
                    Sucesso = false,
                    Mensagem = $"Campos obrigatórios ausentes: {string.Join(", ", missingFields)}",
                    RequisicaoId = requisicaoId,
                    TempoProcessamento = stopwatch.ElapsedMilliseconds
                });
            }

            var pdfBytes = await _pdfEngine.GenerateAsync(templateEntity, request.Dados);
            var nome = string.IsNullOrWhiteSpace(request.NomeArquivo) ? "documento.pdf" : request.NomeArquivo;

            return Ok(new ApiResponse<DocumentResult>
            {
                Sucesso = true,
                RequisicaoId = requisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds,
                Resultado = new DocumentResult
                {
                    Base64 = Convert.ToBase64String(pdfBytes),
                    ContentType = "application/pdf",
                    NomeArquivo = nome
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro em POST /documents/generate-sync (requisicaoId={RequisicaoId})", requisicaoId);
            var detalhe = ex.Message;
            if (ex.InnerException != null)
            {
                detalhe += " | " + ex.InnerException.Message;
            }

            return StatusCode(500, new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = detalhe,
                RequisicaoId = requisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds
            });
        }
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateDocumentRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var requisicaoId = request?.RequisicaoId ?? "";

        try
        {
        Template? templateEntity = null;
        Guid? templateId = null;
        string? templateSnapshotJson = null;

        if (request!.Config.InlineTemplate != null)
        {
            var inline = request.Config.InlineTemplate;
            templateEntity = new Template
            {
                Id = Guid.Empty,
                Slug = "inline",
                Name = "inline",
                Type = inline.Type,
                Content = inline.Content,
                RequiredFields = JsonSerializer.Serialize(inline.RequiredFields ?? new List<string>()),
                IsActive = true
            };
            templateSnapshotJson = JsonSerializer.Serialize(templateEntity);
        }
        else
        {
            templateEntity = await _templateService.GetBySlugAsync(request.Config.Template!);
            if (templateEntity == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Sucesso = false,
                    Mensagem = $"Template '{request.Config.Template}' não encontrado.",
                    RequisicaoId = request.RequisicaoId,
                    TempoProcessamento = stopwatch.ElapsedMilliseconds
                });
            }

            templateId = templateEntity.Id;
        }

        var missingFields = _templateService.ValidateRequiredFields(templateEntity, request.Dados);
        if (missingFields.Any())
        {
            return BadRequest(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = $"Campos obrigatórios ausentes: {string.Join(", ", missingFields)}",
                RequisicaoId = request.RequisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds
            });
        }

        var job = await _documentJobService.CreateAsync(new DocumentJob
        {
            RequisicaoId = request.RequisicaoId,
            TemplateId = templateId,
            TemplateSnapshotJson = templateSnapshotJson,
            CentroCusto = request.Config.CentroCusto,
            NomeArquivo = request.Config.NomeArquivo,
            InputData = JsonSerializer.Serialize(request.Dados),
            Status = "pending"
        });

        _backgroundJobClient.Enqueue<IDocumentWorker>("documents", w => w.ProcessAsync(job.Id));

        return Ok(new ApiResponse<GenerateDocumentResponse>
        {
            Sucesso = true,
            RequisicaoId = request.RequisicaoId,
            TempoProcessamento = stopwatch.ElapsedMilliseconds,
            Resultado = new GenerateDocumentResponse { JobId = job.Id, Status = "queued" }
        });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro em POST /documents/generate (requisicaoId={RequisicaoId})", requisicaoId);
            var detalhe = ex.Message;
            if (ex.InnerException != null)
            {
                detalhe += " | " + ex.InnerException.Message;
            }

            return StatusCode(500, new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = detalhe,
                RequisicaoId = requisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds
            });
        }
    }

    [HttpGet("status/{jobId:guid}")]
    public async Task<IActionResult> GetStatus(Guid jobId)
    {
        var job = await _documentJobService.GetByIdAsync(jobId);
        if (job == null)
        {
            return NotFound();
        }

        var response = new DocumentStatusResponse
        {
            JobId = job.Id,
            Status = job.Status,
            ErrorMessage = job.ErrorMessage
        };

        if (job.Status == "completed" && job.ResultBase64 != null)
        {
            response.Resultado = new DocumentResult
            {
                Base64 = job.ResultBase64,
                ContentType = "application/pdf",
                NomeArquivo = job.NomeArquivo
            };
        }

        return Ok(new ApiResponse<DocumentStatusResponse>
        {
            Sucesso = true,
            RequisicaoId = job.RequisicaoId,
            TempoProcessamento = job.ProcessingTimeMs ?? 0,
            Resultado = response
        });
    }

    /// <summary>Lista jobs recentes (histórico de gerações).</summary>
    [HttpGet("jobs")]
    public async Task<IActionResult> ListJobs(
        [FromQuery] int limit = 100,
        [FromQuery] string? status = null,
        [FromQuery] string? templateType = null,
        [FromQuery] string? search = null)
    {
        var sw = Stopwatch.StartNew();
        var jobs = await _documentJobService.ListRecentAsync(limit, status, templateType, search);
        var items = jobs.Select(j => new DocumentJobListItemDto
        {
            JobId = j.Id,
            RequisicaoId = j.RequisicaoId,
            TemplateSlug = j.Template?.Slug ?? (j.TemplateSnapshotJson != null ? "(inline)" : ""),
            TemplateName = j.Template?.Name ?? (j.TemplateSnapshotJson != null ? "(inline)" : ""),
            TemplateType = j.Template?.Type ?? "",
            CentroCusto = j.CentroCusto,
            NomeArquivo = j.NomeArquivo,
            Status = j.Status,
            ErrorMessage = j.ErrorMessage,
            ProcessingTimeMs = j.ProcessingTimeMs,
            CreatedAt = j.CreatedAt
        }).ToList();

        return Ok(new ApiResponse<IReadOnlyList<DocumentJobListItemDto>>
        {
            Sucesso = true,
            RequisicaoId = Guid.NewGuid().ToString(),
            TempoProcessamento = sw.ElapsedMilliseconds,
            Resultado = items
        });
    }
}
