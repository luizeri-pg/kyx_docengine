using System.Diagnostics;
using System.Text.Json;
using Hangfire;
using KYX.DocEngine.API.Data;
using KYX.DocEngine.API.Models.Entities;
using KYX.DocEngine.API.Services;
using Microsoft.EntityFrameworkCore;

namespace KYX.DocEngine.API.Workers;

public interface IDocumentWorker
{
    Task ProcessAsync(Guid jobId);
}

public class DocumentWorker : IDocumentWorker
{
    private readonly DocEngineDbContext _db;
    private readonly IPdfEngineService _pdfEngine;
    private readonly ILogger<DocumentWorker> _logger;

    public DocumentWorker(DocEngineDbContext db, IPdfEngineService pdfEngine, ILogger<DocumentWorker> logger)
    {
        _db = db;
        _pdfEngine = pdfEngine;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessAsync(Guid jobId)
    {
        var stopwatch = Stopwatch.StartNew();
        var job = await _db.DocumentJobs.Include(j => j.Template).FirstOrDefaultAsync(j => j.Id == jobId);
        if (job == null)
        {
            _logger.LogWarning("Job {JobId} não encontrado.", jobId);
            return;
        }

        job.Status = "processing";
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        try
        {
            var dados = JsonSerializer.Deserialize<Dictionary<string, string>>(job.InputData) ?? new Dictionary<string, string>();
            var template = ResolveTemplate(job);
            var pdfBytes = await _pdfEngine.GenerateAsync(template, dados);
            job.ResultBase64 = Convert.ToBase64String(pdfBytes);
            job.Status = "completed";
            job.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            job.UpdatedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            job.Status = "failed";
            job.ErrorMessage = ex.Message;
            job.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            job.UpdatedAt = DateTime.UtcNow;
            _logger.LogError(ex, "Erro ao processar job {JobId}.", jobId);
        }
        finally
        {
            await _db.SaveChangesAsync();
        }
    }

    private static Template ResolveTemplate(DocumentJob job)
    {
        if (!string.IsNullOrEmpty(job.TemplateSnapshotJson))
        {
            var t = JsonSerializer.Deserialize<Template>(job.TemplateSnapshotJson);
            if (t != null)
            {
                return t;
            }

            throw new InvalidOperationException("Job com templateSnapshotJson inválido.");
        }

        if (job.Template != null)
        {
            return job.Template;
        }

        throw new InvalidOperationException("Job sem template (nem snapshot nem FK).");
    }
}
