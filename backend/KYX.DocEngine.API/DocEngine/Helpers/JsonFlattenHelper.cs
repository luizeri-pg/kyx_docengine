using System.Text.Json;

namespace KYX.DocEngine.API.Helpers;

public static class JsonFlattenHelper
{
    public static Dictionary<string, string> FlattenToStringDictionary(JsonElement element)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return result;

        Flatten(result, string.Empty, element);
        return result;
    }

    private static void Flatten(Dictionary<string, string> result, string prefix, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var next = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    Flatten(result, next, prop.Value);
                }
                break;

            case JsonValueKind.Array:
                var idx = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var next = string.IsNullOrEmpty(prefix) ? idx.ToString() : $"{prefix}.{idx}";
                    Flatten(result, next, item);
                    idx++;
                }
                break;

            case JsonValueKind.String:
                result[prefix] = element.GetString() ?? string.Empty;
                break;

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                result[prefix] = element.ToString();
                break;

            case JsonValueKind.Null:
                result[prefix] = string.Empty;
                break;
        }
    }
}
