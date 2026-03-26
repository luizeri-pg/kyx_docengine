using System.Text.Json;

namespace KYX.DocEngine.API.Helpers;

/// <summary>Interpreta JSON gravado em request_payload/response_payload (DocEngine).</summary>
public static class AuditPayloadHelper
{
    public static string ToRequestPayloadJson(string endpoint, string method, string requestBody)
    {
        return JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["endpoint"] = endpoint,
            ["method"] = method,
            ["body"] = requestBody
        });
    }

    /// <summary>Garante JSON válido para coluna jsonb (resposta pode ser HTML/texto).</summary>
    public static string ToResponsePayloadJson(string responseBody)
    {
        if (string.IsNullOrEmpty(responseBody))
            return "{}";

        try
        {
            JsonDocument.Parse(responseBody);
            return responseBody;
        }
        catch
        {
            return JsonSerializer.Serialize(new { raw = responseBody });
        }
    }

    public static (string Endpoint, string RequestBody) ParseRequestPayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ("", "");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var ep = root.TryGetProperty("endpoint", out var e) ? e.GetString() ?? "" : "";
            var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : json;
            return (ep, body);
        }
        catch
        {
            return ("", json);
        }
    }

    public static string FormatResponsePayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "";

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("raw", out var raw))
                return raw.GetString() ?? json;
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }
}
