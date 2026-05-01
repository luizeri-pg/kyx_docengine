using System.Diagnostics;
using System.Text.Json;
using KYX.DocEngine.API.Helpers;
using KYX.DocEngine.API.Models.DTOs;
using KYX.DocEngine.API.Models.DTOs.Documents;
using KYX.DocEngine.API.Models.Entities;
using KYX.DocEngine.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        ITemplateService templateService,
        IPdfEngineService pdfEngine,
        IPartnerDbFunctionsService partnerDbFunctionsService,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<DocumentsController> logger)
    {
        _templateService = templateService;
        _pdfEngine = pdfEngine;
        _partnerDbFunctionsService = partnerDbFunctionsService;
        _configuration = configuration;
        _environment = environment;
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

        byte[]? pdfIntercaladoSync;
        try
        {
            pdfIntercaladoSync = DecodeOptionalSinglePdfBase64(request.PdfIntercaladoBase64, "pdfIntercaladoBase64");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = ex.Message,
                RequisicaoId = requisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds
            });
        }

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

            var dadosSync = new Dictionary<string, string>(request.Dados, StringComparer.OrdinalIgnoreCase);
            DossieEstruturadaMapper.NormalizeBrandingForGenerate(dadosSync);

            var missingFields = _templateService.ValidateRequiredFields(templateEntity, dadosSync);
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

            var pdfBytes = await _pdfEngine.GenerateAsync(templateEntity, dadosSync, pdfIntercaladoSync);
            var mainPdfPageCount = PdfDossieAnnexFooterStamper.GetPdfPageCount(pdfBytes);
            pdfBytes = AppendAnnexPdfs(pdfBytes, ResolveAnnexPdfsOrdered(request.PdfsAnexos, request.PdfsAnexosBase64));
            pdfBytes = PdfDossieAnnexFooterStamper.StampAnnexPagesIfApplicable(pdfBytes, mainPdfPageCount, dadosSync, _logger);
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = ex.Message,
                RequisicaoId = requisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds
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

        Template? templateEntity = null;
        if (_configuration.GetValue("Documents:DevFileTemplateFallback", false))
        {
            /** Prioridade ao HTML do repo — evita template na BD desactualizado (ex.: {{LOGO_SIMPLIX_BASE64}}). */
            templateEntity = DevFileTemplateFallback.TryLoad(
                _environment.ContentRootPath,
                request.Config.Template!,
                _logger);
        }

        templateEntity ??= await _templateService.GetBySlugAsync(request.Config.Template!);

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

        List<PdfAnexoPayload>? dossieStructuredPdfs = null;
        Dictionary<string, string> dadosFlat;
        var acceptStructured = _configuration.GetValue("Documents:AcceptDossieStructuredPayload", true);
        if (acceptStructured &&
            DossieEstruturadaMapper.TryResolve(
                request.Dados,
                _environment.ContentRootPath,
                _logger,
                out dadosFlat,
                out dossieStructuredPdfs))
        {
            _logger.LogDebug("dados: payload estruturado dossiê (cliente/anexosPdf) convertido para chaves do template.");
        }
        else
        {
            dadosFlat = JsonFlattenHelper.FlattenToStringDictionary(request.Dados);
        }

        DossieEstruturadaMapper.NormalizeBrandingForGenerate(dadosFlat);

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

        byte[]? pdfIntercaladoGen;
        try
        {
            pdfIntercaladoGen = DecodeOptionalSinglePdfBase64(request.Config.PdfIntercaladoBase64, "config.pdfIntercaladoBase64");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = ex.Message,
                RequisicaoId = request.RequisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds
            });
        }

        var pdfBytes = await _pdfEngine.GenerateAsync(templateEntity, dadosFlat, pdfIntercaladoGen);
        var mainPdfPageCount = PdfDossieAnnexFooterStamper.GetPdfPageCount(pdfBytes);
        var annexFromConfig = ResolveAnnexPdfsOrdered(request.Config.PdfsAnexos, request.Config.PdfsAnexosBase64);
        if (annexFromConfig == null || annexFromConfig.Count == 0)
        {
            if (dossieStructuredPdfs is { Count: > 0 })
            {
                annexFromConfig = dossieStructuredPdfs
                    .Where(x => !string.IsNullOrWhiteSpace(x.Base64))
                    .OrderBy(x => x.Ordem ?? int.MaxValue)
                    .Select(x => x.Base64!.Trim())
                    .ToList();
            }
        }

        pdfBytes = AppendAnnexPdfs(pdfBytes, annexFromConfig);
        pdfBytes = PdfDossieAnnexFooterStamper.StampAnnexPagesIfApplicable(pdfBytes, mainPdfPageCount, dadosFlat, _logger);
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

        var skipPersist = _configuration.GetValue("Documents:SkipPartnerDocumentoPersist", false);
        FunctionCallResult dbInsert = new(null, null);
        if (!skipPersist)
        {
            dbInsert = await _partnerDbFunctionsService.InsertDocumentoAsync(
                requestToPersist,
                request.Config.Template!,
                guidArquivo,
                dadosPersist);
        }
        else
        {
            _logger.LogWarning(
                "Documents:SkipPartnerDocumentoPersist=true — PDF gerado sem gravar via ins_tb_documento (apenas para desenvolvimento).");
        }

        if (!skipPersist && !string.IsNullOrWhiteSpace(dbInsert.Erro))
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
            Resultado = new GenerateDocumentResponse
            {
                JobId = guidArquivo,
                Status = "completed",
                Base64 = pdfBase64,
                ContentType = "application/pdf",
                NomeArquivo = request.Config.NomeArquivo
            }
        });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = ex.Message,
                RequisicaoId = requisicaoId,
                TempoProcessamento = stopwatch.ElapsedMilliseconds
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

    private static byte[] AppendAnnexPdfs(byte[] mainPdf, List<string>? pdfsAnexosBase64)
    {
        var annexBytes = PdfAppendHelper.DecodePdfBase64List(pdfsAnexosBase64);
        return annexBytes.Count == 0 ? mainPdf : PdfAppendHelper.AppendAfter(mainPdf, annexBytes);
    }

    /// <summary>
    /// Se <paramref name="ordered"/> tiver itens com Base64, devolve-os ordenados por <c>ordem</c>;
    /// caso contrário usa <paramref name="plainList"/> (ordem = posição no array).
    /// </summary>
    private static List<string>? ResolveAnnexPdfsOrdered(
        IReadOnlyList<PdfAnexoPayload>? ordered,
        List<string>? plainList)
    {
        if (ordered is { Count: > 0 })
        {
            var fromOrdered = ordered
                .Select((item, index) => new { item, index })
                .Where(x => !string.IsNullOrWhiteSpace(x.item.Base64))
                .OrderBy(x => x.item.Ordem ?? int.MaxValue)
                .ThenBy(x => x.index)
                .Select(x => x.item.Base64!.Trim())
                .ToList();
            if (fromOrdered.Count > 0)
            {
                return fromOrdered;
            }
        }

        return plainList;
    }

    private static byte[]? DecodeOptionalSinglePdfBase64(string? base64, string fieldNameForError)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(base64.Trim());
        }
        catch (FormatException ex)
        {
            throw new ArgumentException($"{fieldNameForError} não é Base64 válido.", ex);
        }
    }
}
