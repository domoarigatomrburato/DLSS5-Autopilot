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
}
