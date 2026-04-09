using System.Text.Json;
using Dapper;
using KYX.DocEngine.API.Helpers;
using KYX.DocEngine.API.Models.DTOs.Documents;
using KYX.DocEngine.API.Models.Entities;
using Npgsql;

namespace KYX.DocEngine.API.Services;

public sealed record FunctionCallResult(int? Id, string? Erro);
public sealed record DocumentoDbResult(
    Guid JobId,
    string RequisicaoId,
    string TemplateSlug,
    string NomeArquivo,
    string CentroCusto,
    string? ErrorMessage,
    Dictionary<string, string> Dados);

public sealed record DocumentoListItem(
    Guid JobId,
    string RequisicaoId,
    string TemplateSlug,
    string TemplateName,
    string TemplateType,
    string CentroCusto,
    string NomeArquivo,
    string Status,
    string? ErrorMessage,
    DateTime CreatedAt);

public interface IPartnerDbFunctionsService
{
    Task<FunctionCallResult> InsertTemplateAsync(Template template, int userId = 0, string? key = null);
    Task<Template?> SelectTemplateAsync(string slug, int userId = 0, string? key = null);
    Task<FunctionCallResult> InsertDocumentoAsync(
        GenerateDocumentRequest request,
        string templateSlug,
        Guid guidArquivo,
        object? dadosOverride = null,
        int userId = 0,
        string? key = null);
    Task<DocumentoDbResult?> SelectDocumentoAsync(Guid id, int userId = 0, string? key = null);
    Task<IReadOnlyList<DocumentoListItem>> ListDocumentosAsync(int limit, string? search = null);
}

public class PartnerDbFunctionsService : IPartnerDbFunctionsService
{
    private readonly string _connectionString;
    private readonly ILogger<PartnerDbFunctionsService> _logger;

    public PartnerDbFunctionsService(IConfiguration configuration, ILogger<PartnerDbFunctionsService> logger)
    {
        _connectionString = ConnectionStringHelper.ResolveDefaultConnection(configuration, logger);
        _logger = logger;
    }

    public async Task<FunctionCallResult> InsertTemplateAsync(Template template, int userId = 0, string? key = null)
    {
        var payload = new
        {
            slug = template.Slug,
            name = template.Name,
            type = template.Type,
            content = template.Content,
            requiredFields = ParseRequiredFields(template.RequiredFields)
        };

        await using var db = new NpgsqlConnection(_connectionString);
        var jsonText = await db.ExecuteScalarAsync<string>(
            "SELECT ins_tb_template(@p_user, @p_key, CAST(@p_json AS jsonb))::text",
            new
            {
                p_user = userId,
                p_key = key,
                p_json = JsonSerializer.Serialize(payload)
            });

        return ParseFunctionResult(jsonText);
    }

    public async Task<Template?> SelectTemplateAsync(string slug, int userId = 0, string? key = null)
    {
        await using var db = new NpgsqlConnection(_connectionString);
        var jsonText = await db.ExecuteScalarAsync<string>(
            "SELECT sel_tb_template(@p_user, @p_key, @p_slug)::text",
            new
            {
                p_user = userId,
                p_key = key,
                p_slug = slug
            });

        if (string.IsNullOrWhiteSpace(jsonText))
            return null;

        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Null || root.ValueKind == JsonValueKind.Undefined)
            return null;

        var requiredFieldsJson = root.TryGetProperty("requiredFields", out var requiredFieldsElement)
            ? requiredFieldsElement.GetRawText()
            : "[]";

        return new Template
        {
            Id = TemplateIdentity.ToGuidFromSlug(root.GetProperty("slug").GetString() ?? slug),
            Slug = root.GetProperty("slug").GetString() ?? slug,
            Name = root.TryGetProperty("name", out var name) ? (name.GetString() ?? string.Empty) : string.Empty,
            Type = root.TryGetProperty("type", out var type) ? (type.GetString() ?? "html") : "html",
            Content = root.TryGetProperty("content", out var content) ? (content.GetString() ?? string.Empty) : string.Empty,
            RequiredFields = requiredFieldsJson,
            IsActive = true
        };
    }

    public async Task<FunctionCallResult> InsertDocumentoAsync(
        GenerateDocumentRequest request,
        string templateSlug,
        Guid guidArquivo,
        object? dadosOverride = null,
        int userId = 0,
        string? key = null)
    {
        var centroCustoNormalized = NormalizeUuidText(request.Config.CentroCusto);
        var dadosPayload = dadosOverride ?? ConvertJsonElementToObject(request.Dados);

        var payload = new
        {
            requisicaoId = request.RequisicaoId,
            config = new
            {
                template = templateSlug,
                centroCusto = centroCustoNormalized,
                nomeArquivo = request.Config.NomeArquivo,
                guidArquivo = guidArquivo
            },
            dados = dadosPayload
        };

        await using var db = new NpgsqlConnection(_connectionString);
        var jsonText = await db.ExecuteScalarAsync<string>(
            "SELECT ins_tb_documento(@p_user, @p_key, CAST(@p_json AS jsonb))::text",
            new
            {
                p_user = userId,
                p_key = key,
                p_json = JsonSerializer.Serialize(payload)
            });

        var result = ParseFunctionResult(jsonText);
        if (!string.IsNullOrWhiteSpace(result.Erro))
        {
            _logger.LogWarning("ins_tb_documento retornou erro: {Erro} (requisicaoId={RequisicaoId})", result.Erro, request.RequisicaoId);
        }

        return result;
    }

    private static object ConvertJsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElementToObject(p.Value), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElementToObject).ToList(),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt64(out var i) ? i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => string.Empty,
            _ => element.ToString()
        };
    }

    private static string NormalizeUuidText(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (Guid.TryParse(raw, out var parsed))
            return parsed.ToString();

        return TemplateIdentity.ToGuidFromSlug(raw).ToString();
    }

    public async Task<DocumentoDbResult?> SelectDocumentoAsync(Guid id, int userId = 0, string? key = null)
    {
        await using var db = new NpgsqlConnection(_connectionString);
        var jsonText = await db.ExecuteScalarAsync<string>(
            "SELECT sel_tb_documento(@p_user, @p_key, @p_id)::text",
            new
            {
                p_user = userId,
                p_key = key,
                p_id = id
            });

        if (string.IsNullOrWhiteSpace(jsonText))
            return null;

        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Null || root.ValueKind == JsonValueKind.Undefined)
            return null;

        var config = root.GetProperty("config");
        var dados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("dados", out var dadosElement) && dadosElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in dadosElement.EnumerateObject())
            {
                dados[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? string.Empty
                    : prop.Value.ToString();
            }
        }

        var requisicaoId = root.TryGetProperty("requisicaoId", out var ridEl) ? (ridEl.ToString() ?? string.Empty) : string.Empty;
        var templateSlug = config.TryGetProperty("template", out var tEl) ? (tEl.ToString() ?? string.Empty) : string.Empty;
        var nomeArquivo = config.TryGetProperty("nomeArquivo", out var nEl) ? (nEl.ToString() ?? "documento.pdf") : "documento.pdf";
        var centroCusto = config.TryGetProperty("centroCusto", out var cEl) ? (cEl.ToString() ?? string.Empty) : string.Empty;
        var guidArquivoRaw = config.TryGetProperty("guidArquivo", out var gEl) ? (gEl.ToString() ?? string.Empty) : string.Empty;
        var errorMessage = dados.TryGetValue("str_erro", out var err) ? err : null;

        if (!Guid.TryParse(guidArquivoRaw, out var jobId))
            jobId = id;

        return new DocumentoDbResult(jobId, requisicaoId, templateSlug, nomeArquivo, centroCusto, errorMessage, dados);
    }

    public async Task<IReadOnlyList<DocumentoListItem>> ListDocumentosAsync(int limit, string? search = null)
    {
        limit = Math.Clamp(limit, 1, 500);
        await using var db = new NpgsqlConnection(_connectionString);
        var rows = await db.QueryAsync<DocumentoListItem>(
            @"SELECT
                d.guid_arquivo AS JobId,
                d.guid_request AS RequisicaoId,
                t.str_enum AS TemplateSlug,
                t.str_descricao AS TemplateName,
                t.str_type AS TemplateType,
                d.centro_custo::text AS CentroCusto,
                d.str_descricao AS NomeArquivo,
                CASE WHEN d.str_erro IS NULL OR d.str_erro = '' THEN 'completed' ELSE 'failed' END AS Status,
                d.str_erro AS ErrorMessage,
                COALESCE(d.dh_inclui, NOW()) AS CreatedAt
              FROM tb_documento d
              JOIN tb_template t ON t.id_template = d.id_template
              WHERE (@search IS NULL
                     OR d.guid_request::text ILIKE ('%' || @search || '%')
                     OR d.guid_arquivo::text ILIKE ('%' || @search || '%')
                     OR t.str_enum ILIKE ('%' || @search || '%')
                     OR t.str_descricao ILIKE ('%' || @search || '%')
                     OR d.str_descricao ILIKE ('%' || @search || '%'))
              ORDER BY d.id_documento DESC
              LIMIT @limit",
            new { limit, search = string.IsNullOrWhiteSpace(search) ? null : search.Trim() });

        return rows.ToList();
    }

    private static FunctionCallResult ParseFunctionResult(string? jsonText)
    {
        if (string.IsNullOrWhiteSpace(jsonText))
            return new FunctionCallResult(null, "Resposta vazia da função");

        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;

        int? id = null;
        if (root.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt32(out var parsedId))
            id = parsedId;

        string? erro = null;
        if (root.TryGetProperty("erro", out var errEl) && errEl.ValueKind != JsonValueKind.Null)
            erro = errEl.GetString();

        return new FunctionCallResult(id, erro);
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
}

internal static class TemplateIdentity
{
    public static Guid ToGuidFromSlug(string slug)
    {
        var normalized = (slug ?? string.Empty).Trim().ToLowerInvariant();
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(normalized));
        return new Guid(hash);
    }
}
