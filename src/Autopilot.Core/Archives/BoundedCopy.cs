namespace Autopilot.Core.Archives;

/// <summary>Writes one member, refusing it as soon as it yields more than its header declared.</summary>
internal static class BoundedCopy
{
    private const int ChunkSize = 81_920;

    public static async Task WriteAsync(Stream source, string target, long declared, string archivePath, string memberPath, CancellationToken ct)
    {
        string? folder = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(folder))
        {
            _ = Directory.CreateDirectory(folder);
        }

        await using FileStream output = new(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, ChunkSize, useAsync: true);
        byte[] buffer = new byte[ChunkSize];
        long written = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            written += read;
            if (written > declared)
            {
                throw ArchiveException.For(ArchiveFailure.TooLarge, archivePath, $"member '{memberPath}' yields more than the {declared} bytes its header declares");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }
}
