using System.Diagnostics;
using System.Text.Json;
using KYX.DocEngine.API.Models.DTOs;
using KYX.DocEngine.API.Models.DTOs.Templates;
using KYX.DocEngine.API.Models.Entities;
using KYX.DocEngine.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.IO;

namespace KYX.DocEngine.API.Controllers;

[ApiController]
[Authorize]
[Route("templates")]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateService _templateService;

    public TemplatesController(ITemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var stopwatch = Stopwatch.StartNew();
        var templates = await _templateService.ListActiveAsync();
        return Ok(new ApiResponse<IEnumerable<TemplateResponse>>
        {
            Sucesso = true,
            RequisicaoId = Guid.NewGuid().ToString(),
            TempoProcessamento = stopwatch.ElapsedMilliseconds,
            Resultado = templates.Select(t => ToResponse(t, includeContent: false))
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var stopwatch = Stopwatch.StartNew();
        var template = await _templateService.GetByIdAsync(id);
        if (template == null)
        {
            return NotFound();
        }

        return Ok(new ApiResponse<TemplateResponse>
        {
            Sucesso = true,
            RequisicaoId = Guid.NewGuid().ToString(),
            TempoProcessamento = stopwatch.ElapsedMilliseconds,
            Resultado = ToResponse(template, includeContent: true)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertTemplateRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var entity = new Template
        {
            Slug = request.Slug,
            Name = request.Name,
            Type = request.Type,
            Content = request.Content,
            RequiredFields = JsonSerializer.Serialize(request.RequiredFields),
            IsActive = true
        };
        var created = await _templateService.CreateAsync(entity);
        return Ok(new ApiResponse<TemplateResponse>
        {
            Sucesso = true,
            RequisicaoId = Guid.NewGuid().ToString(),
            TempoProcessamento = stopwatch.ElapsedMilliseconds,
            Resultado = ToResponse(created, includeContent: true)
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertTemplateRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var updated = await _templateService.UpdateAsync(id, new Template
        {
            Slug = request.Slug,
            Name = request.Name,
            Type = request.Type,
            Content = request.Content,
            RequiredFields = JsonSerializer.Serialize(request.RequiredFields),
            IsActive = true
        });
        if (updated == null)
        {
            return NotFound();
        }

        return Ok(new ApiResponse<TemplateResponse>
        {
            Sucesso = true,
            RequisicaoId = Guid.NewGuid().ToString(),
            TempoProcessamento = stopwatch.ElapsedMilliseconds,
            Resultado = ToResponse(updated, includeContent: true)
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var stopwatch = Stopwatch.StartNew();
        var deleted = await _templateService.SoftDeleteAsync(id);
        if (!deleted)
        {
            return NotFound();
        }

        return Ok(new ApiResponse<object>
        {
            Sucesso = true,
            RequisicaoId = Guid.NewGuid().ToString(),
            TempoProcessamento = stopwatch.ElapsedMilliseconds,
            Resultado = null
        });
    }

    [HttpPost("inspect-pdf")]
    public IActionResult InspectPdf([FromBody] InspectPdfRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var pdfBytes = Convert.FromBase64String(request.PdfBase64);
        using var stream = new MemoryStream(pdfBytes);
        var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        var fields = new List<string>();
        if (document.AcroForm?.Fields != null)
        {
            foreach (PdfAcroField field in document.AcroForm.Fields)
            {
                fields.Add(field.Name);
            }
        }

        return Ok(new ApiResponse<object>
        {
            Sucesso = true,
            RequisicaoId = Guid.NewGuid().ToString(),
            TempoProcessamento = stopwatch.ElapsedMilliseconds,
            Resultado = new { fields }
        });
    }

    private static TemplateResponse ToResponse(Template template, bool includeContent)
    {
        return new TemplateResponse
        {
            Id = template.Id,
            Slug = template.Slug,
            Name = template.Name,
            Type = template.Type,
            Content = includeContent ? template.Content : null,
            RequiredFields = template.RequiredFields,
            IsActive = template.IsActive
        };
    }
}
