namespace Autopilot.Core.Storage;

/// <summary>Replaces a file whole, so a crash or a failed write never leaves it half written.</summary>
public static class AtomicFile
{
    /// <summary>
    /// Writes the content <paramref name="write"/> produces to a temporary file in the target's
    /// folder, flushes it to disk, then moves it over <paramref name="path"/>, creating the folder
    /// when needed. On any failure the temporary file is removed and the old file is left whole.
    /// </summary>
    /// <exception cref="IOException">The write or the move failed.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder or the target cannot be written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task WriteAsync(string path, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);
        string folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        _ = Directory.CreateDirectory(folder);
        string temporary = Path.Combine(folder, Path.GetRandomFileName() + ".tmp");
        try
        {
            await using (FileStream file = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await write(file, cancellationToken);
                await file.FlushAsync(cancellationToken);
                // There is no asynchronous fsync, so this blocks; without it a crash after the move
                // can leave an empty file under the target's name.
                RandomAccess.FlushToDisk(file.SafeFileHandle);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }
}
