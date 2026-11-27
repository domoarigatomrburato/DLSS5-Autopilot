using System.Text.Json;
using Autopilot.Core.Archives;
using Autopilot.Core.Platform;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Library.Stores;

/// <summary>Reads the files stores write, as untrusted input: capped in size, and nothing when unreadable or malformed.</summary>
internal static class StoreFiles
{
    /// <summary>A store file larger than this is not one the store wrote.</summary>
    public const int MaxBytes = 16 * 1024 * 1024;

    /// <summary>The file's bytes; null when it is missing, oversize or cannot be read.</summary>
    public static Task<byte[]?> ReadAsync(string path, CancellationToken cancellationToken) => BoundedFile.ReadAsync(path, MaxBytes, cancellationToken);

    /// <summary>The parsed JSON file; null when it is missing, oversize, unreadable or malformed.</summary>
    public static Task<JsonDocument?> ReadJsonAsync(string path, CancellationToken cancellationToken) => UntrustedJson.ReadAsync(path, MaxBytes, cancellationToken);

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

    /// <summary>The text trimmed; null when it is missing or blank.</summary>
    public static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>The last part of a folder path, ignoring a trailing separator of either kind.</summary>
    public static string FolderName(string folder) => Path.GetFileName(folder.TrimEnd('\\', '/'));
}
