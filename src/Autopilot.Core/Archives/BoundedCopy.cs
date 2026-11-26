using System.Buffers;

namespace Autopilot.Core.Archives;

/// <summary>
/// Writes the members of one extraction through one pooled buffer, refusing a member as soon as it
/// yields more than its header declared.
/// </summary>
internal sealed class BoundedCopy(string archivePath) : IDisposable
{
    private const int ChunkSize = 81_920;

    private readonly byte[] _buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);

    public async Task WriteAsync(Stream source, string target, long declared, string memberPath, CancellationToken ct)
    {
        string? folder = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(folder))
        {
            _ = Directory.CreateDirectory(folder);
        }

        // The pooled buffer already batches the writes, so the file keeps none of its own.
        await using FileStream output = new(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 0, useAsync: true);
        long written = 0;
        int read;
        while ((read = await source.ReadAsync(_buffer, ct)) > 0)
        {
            written += read;
            if (written > declared)
            {
                throw ArchiveException.For(ArchiveFailure.TooLarge, archivePath, $"member '{memberPath}' yields more than the {declared} bytes its header declares");
            }

            await output.WriteAsync(_buffer.AsMemory(0, read), ct);
        }
    }

    public void Dispose() => ArrayPool<byte>.Shared.Return(_buffer);
}
