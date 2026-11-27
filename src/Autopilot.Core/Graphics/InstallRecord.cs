using System.Collections.Frozen;
using System.Text.Json;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Graphics;

/// <summary>
/// The file names this tool's install record in a game's folder says it wrote, read as untrusted JSON:
/// a stand-in until M4's install record and its reader replace it.
/// </summary>
internal static class InstallRecord
{
    /// <summary>A record larger than this is not one of ours.</summary>
    private const int MaxBytes = 1024 * 1024;

    /// <summary>The names under <c>files</c>, by file name; empty for a missing, oversize or malformed record.</summary>
    public static async Task<FrozenSet<string>> WrittenAsync(string folder, CancellationToken cancellationToken)
    {
        using JsonDocument? record = await UntrustedJson.ReadAsync(Path.Join(folder, KnownFiles.InstallRecord), MaxBytes, cancellationToken);
        return record is null ? [] : Names(record.RootElement);
    }

    private static FrozenSet<string> Names(JsonElement root) =>
        UntrustedJson.Array(root, "files")
            .Where(file => file.ValueKind == JsonValueKind.String)
            .Select(file => FileName(file.GetString()!))
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    // Recorded as relative paths with either kind of slash; compared by file name.
    private static string FileName(string recorded) => recorded[(recorded.LastIndexOfAny(['/', '\\']) + 1)..];
}
