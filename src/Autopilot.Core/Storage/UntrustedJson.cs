using System.Text.Json;

namespace Autopilot.Core.Storage;

/// <summary>Reads JSON that is untrusted input: capped in size, and nothing when unreadable, malformed or of another shape.</summary>
internal static class UntrustedJson
{
    /// <summary>The parsed JSON file; null when it is missing, larger than <paramref name="maxBytes"/>, unreadable or malformed.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<JsonDocument?> ReadAsync(string path, long maxBytes, CancellationToken cancellationToken)
    {
        byte[]? bytes = await BoundedFile.ReadAsync(path, maxBytes, cancellationToken);
        if (bytes is null)
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(Utf8(bytes));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A JSON file's bytes without the UTF-8 byte order mark some tools save, which the parsers refuse.</summary>
    public static ReadOnlyMemory<byte> Utf8(byte[] bytes) => bytes.AsMemory(bytes is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0);

    /// <summary>A string property of a JSON object; null when absent or of another type.</summary>
    public static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>A boolean property of a JSON object; null when absent or of another type.</summary>
    public static bool? Flag(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    /// <summary>The elements of an array property of a JSON object; none when absent or of another type.</summary>
    public static IEnumerable<JsonElement> Array(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];
}
