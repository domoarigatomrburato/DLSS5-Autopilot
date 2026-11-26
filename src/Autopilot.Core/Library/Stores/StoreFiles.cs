using System.Text.Json;
using Autopilot.Core.Archives;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>Reads the files stores write, as untrusted input: capped in size, and nothing when unreadable or malformed.</summary>
internal static class StoreFiles
{
    /// <summary>A store file larger than this is not one the store wrote.</summary>
    public const int MaxBytes = 16 * 1024 * 1024;

    /// <summary>The file's bytes; null when it is missing, oversize or cannot be read.</summary>
    public static async Task<byte[]?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            FileInfo file = new(path);
            if (!file.Exists || file.Length > MaxBytes)
            {
                return null;
            }

            return await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return null;
        }
    }

    /// <summary>The parsed JSON file; null when it is missing, oversize, unreadable or malformed.</summary>
    public static async Task<JsonDocument?> ReadJsonAsync(string path, CancellationToken cancellationToken)
    {
        byte[]? bytes = await ReadAsync(path, cancellationToken);
        if (bytes is null)
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(bytes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

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

    /// <summary>
    /// The store's own executable, a path relative to the game folder, as a full path when it is a safe
    /// relative name and the file exists; else null.
    /// </summary>
    public static string? ExistingExecutable(string folder, string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            return null;
        }

        try
        {
            string path = SafePath.Join(folder, relative);
            return DrivePaths.IsFile(path) ? path : null;
        }
        catch (UnsafePathException)
        {
            return null;
        }
    }

    /// <summary>The last part of a folder path, ignoring a trailing separator of either kind.</summary>
    public static string FolderName(string folder) => Path.GetFileName(folder.TrimEnd('\\', '/'));
}
