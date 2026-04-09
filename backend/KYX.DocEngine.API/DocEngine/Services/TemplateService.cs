using System.Text.Json;
using Dapper;
using KYX.DocEngine.API.Helpers;
using KYX.DocEngine.API.Models.Entities;
using Npgsql;

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
    private readonly string _connectionString;
    private readonly IPartnerDbFunctionsService _partnerDbFunctionsService;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(
        IConfiguration configuration,
        IPartnerDbFunctionsService partnerDbFunctionsService,
        ILogger<TemplateService> logger)
    {
        _connectionString = ConnectionStringHelper.ResolveDefaultConnection(configuration, logger);
        _partnerDbFunctionsService = partnerDbFunctionsService;
        _logger = logger;
    }

    public async Task<List<Template>> ListActiveAsync()
    {
        try
        {
            await using var db = new NpgsqlConnection(_connectionString);
            var rows = await db.QueryAsync<TemplateDbRow>(
                @"SELECT
                    id_template AS IdTemplate,
                    str_enum AS StrEnum,
                    str_descricao AS StrDescricao,
                    str_type AS StrType,
                    str_content AS StrContent,
                    campos AS Campos
                  FROM tb_template
                  ORDER BY id_template DESC");

            return rows.Select(MapRow).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao listar templates em tb_template. Retornando lista vazia.");
            return new List<Template>();
        }
    }

    public async Task<Template?> GetByIdAsync(Guid id)
    {
        var templates = await ListActiveAsync();
        return templates.FirstOrDefault(t => t.Id == id);
    }

    public Task<Template?> GetBySlugAsync(string slug) =>
        _partnerDbFunctionsService.SelectTemplateAsync(slug);

    public async Task<Template> CreateAsync(Template template)
    {
        var result = await _partnerDbFunctionsService.InsertTemplateAsync(template);
        if (!string.IsNullOrWhiteSpace(result.Erro))
        {
            throw new InvalidOperationException(result.Erro);
        }

        return await GetBySlugAsync(template.Slug) ?? template;
    }

    public async Task<Template?> UpdateAsync(Guid id, Template payload)
    {
        var current = await GetByIdAsync(id);
        if (current == null)
        {
            return null;
        }

        await using var db = new NpgsqlConnection(_connectionString);
        var affected = await db.ExecuteAsync(
            @"UPDATE tb_template
              SET str_enum = @newSlug,
                  str_descricao = @name,
                  str_type = @type,
                  str_content = @content,
                  campos = @requiredFields
              WHERE str_enum = @currentSlug",
            new
            {
                newSlug = payload.Slug,
                name = payload.Name,
                type = payload.Type,
                content = payload.Content,
                requiredFields = ParseRequiredFields(payload.RequiredFields),
                currentSlug = current.Slug
            });

        if (affected == 0)
        {
            return null;
        }

        return await GetBySlugAsync(payload.Slug);
    }

    public async Task<bool> SoftDeleteAsync(Guid id)
    {
        var current = await GetByIdAsync(id);
        if (current == null)
        {
            return false;
        }

        await using var db = new NpgsqlConnection(_connectionString);
        var affected = await db.ExecuteAsync(
            "DELETE FROM tb_template WHERE str_enum = @slug",
            new { slug = current.Slug });
        return affected > 0;
    }

    public List<string> ValidateRequiredFields(Template template, Dictionary<string, string> dados)
    {
        var requiredFields = JsonSerializer.Deserialize<List<string>>(template.RequiredFields) ?? new List<string>();
        return requiredFields.Where(field => !dados.ContainsKey(field) || string.IsNullOrWhiteSpace(dados[field])).ToList();
    }

    private static Template MapRow(TemplateDbRow row)
    {
        return new Template
        {
            Id = TemplateIdentity.ToGuidFromSlug(row.StrEnum ?? string.Empty),
            Slug = row.StrEnum ?? string.Empty,
            Name = row.StrDescricao ?? string.Empty,
            Type = row.StrType ?? "html",
            Content = row.StrContent ?? string.Empty,
            RequiredFields = JsonSerializer.Serialize(row.Campos ?? Array.Empty<string>()),
            IsActive = true
        };
    }

    private static List<string> ParseRequiredFields(string requiredFieldsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(requiredFieldsJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private sealed class TemplateDbRow
    {
        public int IdTemplate { get; init; }
        public string? StrEnum { get; init; }
        public string? StrDescricao { get; init; }
        public string? StrType { get; init; }
        public string? StrContent { get; init; }
        public string[]? Campos { get; init; }
    }
}
