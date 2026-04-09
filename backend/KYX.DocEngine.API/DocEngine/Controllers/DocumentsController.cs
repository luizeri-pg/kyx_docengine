using System.Diagnostics;
using System.Text.Json;
using KYX.DocEngine.API.Helpers;
using KYX.DocEngine.API.Models.DTOs;
using KYX.DocEngine.API.Models.DTOs.Documents;
using KYX.DocEngine.API.Models.Entities;
using KYX.DocEngine.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KYX.DocEngine.API.Controllers;

[ApiController]
[Authorize]
[Route("documents")]
public class DocumentsController : ControllerBase
{
    private readonly ITemplateService _templateService;
    private readonly IPdfEngineService _pdfEngine;
    private readonly IPartnerDbFunctionsService _partnerDbFunctionsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        ITemplateService templateService,
        IPdfEngineService pdfEngine,
        IPartnerDbFunctionsService partnerDbFunctionsService,
        IConfiguration configuration,
        ILogger<DocumentsController> logger)
    {
        _templateService = templateService;
        _pdfEngine = pdfEngine;
        _partnerDbFunctionsService = partnerDbFunctionsService;
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
        if (request!.Config.InlineTemplate != null)
        {
            return BadRequest(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = "No padrão atual do BD, use config.template (slug existente em tb_template). inlineTemplate não é suportado neste endpoint.",
                RequisicaoId = request.RequisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds
            });
        }

        var templateEntity = await _templateService.GetBySlugAsync(request.Config.Template!);
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

        var dadosFlat = JsonFlattenHelper.FlattenToStringDictionary(request.Dados);
        var missingFields = _templateService.ValidateRequiredFields(templateEntity, dadosFlat);
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

        var jobId = Guid.NewGuid();
        if (!Guid.TryParse(request.RequisicaoId, out _))
        {
            return BadRequest(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = "requisicaoId deve ser um GUID válido para seguir o padrão do BD.",
                RequisicaoId = request.RequisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds
            });
        }

        var guidArquivo = jobId;
        if (!string.IsNullOrWhiteSpace(request.Config.GuidArquivo) &&
            !Guid.TryParse(request.Config.GuidArquivo, out guidArquivo))
        {
            return BadRequest(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = "config.guidArquivo inválido. Informe um GUID válido.",
                RequisicaoId = request.RequisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds
            });
        }

        var pdfBytes = await _pdfEngine.GenerateAsync(templateEntity, dadosFlat);
        var pdfBase64 = Convert.ToBase64String(pdfBytes);
        var dadosPersist = JsonSerializer.Deserialize<Dictionary<string, object?>>(
            request.Dados.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        dadosPersist["_pdfBase64"] = pdfBase64;

        var requestToPersist = new GenerateDocumentRequest
        {
            RequisicaoId = request.RequisicaoId,
            Config = request.Config,
            Dados = request.Dados
        };

        var dbInsert = await _partnerDbFunctionsService.InsertDocumentoAsync(
            requestToPersist,
            request.Config.Template!,
            guidArquivo,
            dadosPersist);

        if (!string.IsNullOrWhiteSpace(dbInsert.Erro))
        {
            return BadRequest(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = dbInsert.Erro,
                RequisicaoId = request.RequisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds
            });
        }

        return Ok(new ApiResponse<GenerateDocumentResponse>
        {
            Sucesso = true,
            RequisicaoId = request.RequisicaoId,
            TempoProcessamento = stopwatch.ElapsedMilliseconds,
            Resultado = new GenerateDocumentResponse { JobId = guidArquivo, Status = "completed" }
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
        var documento = await _partnerDbFunctionsService.SelectDocumentoAsync(jobId);
        if (documento == null)
        {
            return NotFound();
        }

        documento.Dados.TryGetValue("_pdfBase64", out var base64);
        var response = new DocumentStatusResponse
        {
            JobId = documento.JobId,
            Status = string.IsNullOrWhiteSpace(base64) ? "processing" : "completed",
            ErrorMessage = documento.ErrorMessage
        };

        if (!string.IsNullOrWhiteSpace(base64))
        {
            response.Resultado = new DocumentResult
            {
                Base64 = base64,
                ContentType = "application/pdf",
                NomeArquivo = documento.NomeArquivo
            };
        }

        return Ok(new ApiResponse<DocumentStatusResponse>
        {
            Sucesso = true,
            RequisicaoId = documento.RequisicaoId,
            TempoProcessamento = 0,
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
        var docs = await _partnerDbFunctionsService.ListDocumentosAsync(limit, search);
        var items = docs.Select(j => new DocumentJobListItemDto
        {
            JobId = j.JobId,
            RequisicaoId = j.RequisicaoId,
            TemplateSlug = j.TemplateSlug,
            TemplateName = j.TemplateName,
            TemplateType = j.TemplateType,
            CentroCusto = j.CentroCusto,
            NomeArquivo = j.NomeArquivo,
            Status = j.Status,
            ErrorMessage = j.ErrorMessage,
            ProcessingTimeMs = null,
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
