using System.Text.Json;
using KYX.DocEngine.API.Data;
using KYX.DocEngine.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KYX.DocEngine.API.Services;

public interface ITemplateService
{
    Task<List<Template>> ListActiveAsync();
    Task<Template?> GetByIdAsync(Guid id);
    Task<Template?> GetBySlugAsync(string slug);
    Task<Template> CreateAsync(Template template);
    Task<Template?> UpdateAsync(Guid id, Template payload);
    Task<bool> SoftDeleteAsync(Guid id);
    List<string> ValidateRequiredFields(Template template, Dictionary<string, string> dados);
}

public class TemplateService : ITemplateService
{
    private readonly DocEngineDbContext _db;

    public TemplateService(DocEngineDbContext db)
    {
        _db = db;
    }

    public Task<List<Template>> ListActiveAsync() =>
        _db.Templates.Where(t => t.IsActive).OrderByDescending(t => t.CreatedAt).ToListAsync();

    public Task<Template?> GetByIdAsync(Guid id) => _db.Templates.FirstOrDefaultAsync(t => t.Id == id);

    public Task<Template?> GetBySlugAsync(string slug) =>
        _db.Templates.FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive);

    public async Task<Template> CreateAsync(Template template)
    {
        _db.Templates.Add(template);
        await _db.SaveChangesAsync();
        return template;
    }

    public async Task<Template?> UpdateAsync(Guid id, Template payload)
    {
        var current = await GetByIdAsync(id);
        if (current == null)
        {
            return null;
        }

        current.Slug = payload.Slug;
        current.Name = payload.Name;
        current.Type = payload.Type;
        current.Content = payload.Content;
        current.RequiredFields = payload.RequiredFields;
        current.IsActive = payload.IsActive;
        current.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return current;
    }

    public async Task<bool> SoftDeleteAsync(Guid id)
    {
        var current = await GetByIdAsync(id);
        if (current == null)
        {
            return false;
        }

        current.IsActive = false;
        current.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public List<string> ValidateRequiredFields(Template template, Dictionary<string, string> dados)
    {
        var requiredFields = JsonSerializer.Deserialize<List<string>>(template.RequiredFields) ?? new List<string>();
        return requiredFields.Where(field => !dados.ContainsKey(field) || string.IsNullOrWhiteSpace(dados[field])).ToList();
    }
}
