using System.Security.Cryptography;

namespace Autopilot.Core.Storage;

/// <summary>The SHA-256 of a file on disk, streamed: the one hash every check of a file's content uses.</summary>
internal static class FileDigest
{
    private const int BufferSize = 1024 * 1024;

    /// <summary>The SHA-256 of the file at <paramref name="path"/>, as lowercase hex.</summary>
    /// <exception cref="IOException">The file cannot be read (missing, locked).</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be opened.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        // A large buffer, so Windows issues one overlapped read per mebibyte rather than per 4 KB.
        await using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken));
    }
}
