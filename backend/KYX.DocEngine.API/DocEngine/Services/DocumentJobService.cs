using KYX.DocEngine.API.Data;
using KYX.DocEngine.API.Helpers;
using KYX.DocEngine.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KYX.DocEngine.API.Services;

public interface IDocumentJobService
{
    Task<DocumentJob> CreateAsync(DocumentJob job);
    Task<DocumentJob?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<DocumentJob>> ListRecentAsync(int limit, string? status, string? templateType, string? search);
}

public class DocumentJobService : IDocumentJobService
{
    private readonly DocEngineDbContext _db;
    private readonly ILogger<DocumentJobService> _logger;

    public DocumentJobService(DocEngineDbContext db, ILogger<DocumentJobService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DocumentJob> CreateAsync(DocumentJob job)
    {
        _db.DocumentJobs.Add(job);
        await _db.SaveChangesAsync();
        return job;
    }

    public Task<DocumentJob?> GetByIdAsync(Guid id) =>
        _db.DocumentJobs.Include(j => j.Template).FirstOrDefaultAsync(j => j.Id == id);

    public async Task<IReadOnlyList<DocumentJob>> ListRecentAsync(int limit, string? status, string? templateType, string? search)
    {
        limit = Math.Clamp(limit, 1, 500);
        try
        {
            var q = _db.DocumentJobs.Include(j => j.Template).AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
                q = q.Where(j => j.Status == status);

            if (!string.IsNullOrWhiteSpace(templateType) && !string.Equals(templateType, "all", StringComparison.OrdinalIgnoreCase))
                q = q.Where(j => j.Template != null && j.Template.Type == templateType);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(j =>
                    j.RequisicaoId.Contains(s)
                    || j.NomeArquivo.Contains(s)
                    || (j.Template != null && (j.Template.Slug.Contains(s) || j.Template.Name.Contains(s)))
                    || j.CentroCusto.Contains(s));
            }

            return await q
                .OrderByDescending(j => j.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex) when (PostgresErrors.IsUndefinedTable(ex))
        {
            _logger.LogWarning(
                ex,
                "Tabela «document_jobs» (ou «templates») ausente. Ative migrações ou execute «dotnet ef database update». Devolvendo lista vazia.");
            return Array.Empty<DocumentJob>();
        }
    }
}
