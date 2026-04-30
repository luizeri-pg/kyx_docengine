using System.Text.Json;
using KYX.DocEngine.API.Models.Entities;

namespace KYX.DocEngine.API.Helpers;

/// <summary>
/// Ambiente local sem <c>tb_template</c> populada: carrega o HTML do repositório (docs/templates).
/// Só é usado quando <c>Documents:DevFileTemplateFallback</c> está activo (tipicamente Development).
/// </summary>
public static class DevFileTemplateFallback
{
    private static readonly Dictionary<string, string> SlugToRelativeHtmlPath =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["dossie-simplix-v2"] = "../../docs/templates/template-dossie-simplix.html",
            ["dossie-simplix-v3"] = "../../docs/templates/template-dossie-simplix.html",
            ["dossie-simplix"] = "../../docs/templates/template-dossie-simplix.html",
            ["simplix-dossie"] = "../../docs/templates/template-dossie-simplix.html"
        };

    public static Template? TryLoad(string contentRootPath, string slug, ILogger? logger = null)
    {
        var s = (slug ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(s) || !SlugToRelativeHtmlPath.TryGetValue(s, out var rel))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(contentRootPath, rel));
        if (!File.Exists(path))
        {
            logger?.LogWarning("DevFileTemplateFallback: HTML não encontrado em {Path} (slug={Slug}).", path, s);
            return null;
        }

        var html = File.ReadAllText(path);
        logger?.LogInformation(
            "DevFileTemplateFallback: a usar HTML local para slug={Slug} ({Path}, {Len} chars).",
            s,
            path,
            html.Length);

        return new Template
        {
            Id = Guid.Empty,
            Slug = s,
            Name = $"Dev file ({s})",
            Type = "html",
            Content = html,
            RequiredFields = JsonSerializer.Serialize(Array.Empty<string>()),
            IsActive = true
        };
    }
}
