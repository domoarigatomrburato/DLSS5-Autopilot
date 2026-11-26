using System.Collections.Frozen;
using System.Text.Json;

namespace Autopilot.Core.Graphics;

/// <summary>The file names this tool's install record in a game's folder says it wrote, read as untrusted JSON.</summary>
internal static class InstallRecord
{
    /// <summary>A record larger than this is not one of ours.</summary>
    public const int MaxBytes = 1024 * 1024;

    /// <summary>The names under <c>files</c>, by file name; empty for a missing, oversize or malformed record.</summary>
    public static async Task<FrozenSet<string>> WrittenAsync(string folder, CancellationToken cancellationToken)
    {
        string path = Path.Join(folder, KnownFiles.InstallRecord);
        try
        {
            FileInfo file = new(path);
            if (!file.Exists || file.Length > MaxBytes)
            {
                return [];
            }

            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            using var record = JsonDocument.Parse(bytes);
            return Names(record.RootElement);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    private static FrozenSet<string> Names(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out JsonElement files) || files.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return files.EnumerateArray()
            .Where(file => file.ValueKind == JsonValueKind.String)
            .Select(file => FileName(file.GetString()!))
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    // Recorded as relative paths with either kind of slash; compared by file name.
    private static string FileName(string recorded) => recorded[(recorded.LastIndexOfAny(['/', '\\']) + 1)..];
}
