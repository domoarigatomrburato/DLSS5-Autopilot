using Autopilot.Core.Platform;

namespace Autopilot.Core.Storage;

/// <summary>Reads a whole file that is untrusted input, capped in size.</summary>
internal static class BoundedFile
{
    private const int ChunkSize = 81_920;

    /// <summary>
    /// The file's bytes; null when it is missing, larger than <paramref name="maxBytes"/> (even if it
    /// grows while being read) or cannot be read.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<byte[]?> ReadAsync(string path, long maxBytes, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            if (stream.Length > maxBytes)
            {
                return null;
            }

            using MemoryStream content = new((int)stream.Length);
            byte[] chunk = new byte[ChunkSize];
            int read;
            while ((read = await stream.ReadAsync(chunk.AsMemory(0, (int)Math.Min(ChunkSize, maxBytes + 1 - content.Length)), cancellationToken)) > 0)
            {
                await content.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
                if (content.Length > maxBytes)
                {
                    return null;
                }
            }

            return content.ToArray();
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return null;
        }
    }

    /// <summary>
    /// The file's bytes, read now, telling apart a missing file, one larger than
    /// <paramref name="maxBytes"/> (even if it grows while being read), a folder where the file
    /// should be, and one that cannot be read.
    /// </summary>
    public static FileBytes Read(string path, long maxBytes)
    {
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > maxBytes)
            {
                return new(FileBytesState.TooLarge, null);
            }

            using MemoryStream content = new((int)stream.Length);
            byte[] chunk = new byte[ChunkSize];
            int read;
            while ((read = stream.Read(chunk.AsSpan(0, (int)Math.Min(ChunkSize, maxBytes + 1 - content.Length)))) > 0)
            {
                content.Write(chunk.AsSpan(0, read));
                if (content.Length > maxBytes)
                {
                    return new(FileBytesState.TooLarge, null);
                }
            }

            return new(FileBytesState.Read, content.ToArray());
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return new(FileBytesState.Missing, null);
        }
        catch (UnauthorizedAccessException) when (Directory.Exists(path))
        {
            // Opening a folder as a file is denied, as a locked file's open can be; only this one stays so.
            return new(FileBytesState.Folder, null);
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return new(FileBytesState.Unreadable, null);
        }
    }
}

/// <summary>What <see cref="BoundedFile.Read"/> found; <see cref="Bytes"/> is set when the file was read.</summary>
internal sealed record FileBytes(FileBytesState State, byte[]? Bytes);

/// <summary>How a bounded read of a file ended.</summary>
internal enum FileBytesState
{
    /// <summary>Read whole.</summary>
    Read,

    /// <summary>There is no such file (or folder on its path).</summary>
    Missing,

    /// <summary>Larger than the limit.</summary>
    TooLarge,

    /// <summary>A folder, not a file.</summary>
    Folder,

    /// <summary>There, but it could not be read: locked, denied, or on a drive that is not ready.</summary>
    Unreadable,
}
