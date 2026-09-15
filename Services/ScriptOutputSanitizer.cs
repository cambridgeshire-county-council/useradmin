using System.Text.Json;
using System.Text.RegularExpressions;

namespace PSScriptWebApp.Services;

public static class ScriptOutputSanitizer
{
    public static string? SanitizeOutput(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return output;

        return Regex.Replace(
            output,
            @"(?im)^Generated Password:\s*.+$",
            "Generated Password: [hidden]");
    }

    public static string SanitizeSseEvent(string sseEvent)
    {
        if (!sseEvent.Contains("Generated Password", StringComparison.OrdinalIgnoreCase) ||
            !sseEvent.StartsWith("data:"))
            return sseEvent;

        try
        {
            var jsonPart = sseEvent["data:".Length..].TrimEnd('\n');
            using var document = JsonDocument.Parse(jsonPart);
            var root = document.RootElement;
            var type = root.GetProperty("type").GetString() ?? "line";
            if (root.TryGetProperty("text", out var textProperty))
            {
                var sanitised = SanitizeOutput(textProperty.GetString());
                return "data:" + JsonSerializer.Serialize(new { type, text = sanitised }) + "\n\n";
            }
        }
        catch (JsonException) { }

        return sseEvent;
    }
}