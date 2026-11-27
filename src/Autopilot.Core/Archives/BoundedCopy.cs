using System.Buffers;

namespace Autopilot.Core.Archives;

/// <summary>
/// Writes the members of one extraction through one pooled buffer, refusing a member as soon as it
/// yields more than its header declared. The buffer is rented on the first write, so a read that
/// writes nothing, such as a listing, rents none.
/// </summary>
internal sealed class BoundedCopy(string archivePath) : IDisposable
{
    private const int ChunkSize = 81_920;

    private byte[]? _buffer;

    public async Task WriteAsync(Stream source, string target, long declared, string memberPath, CancellationToken ct)
    {
        string? folder = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(folder))
        {
            _ = Directory.CreateDirectory(folder);
        }

        // The pooled buffer already batches the writes, so the file keeps none of its own.
        await using FileStream output = new(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 0, useAsync: true);
        byte[] buffer = Buffer;
        long written = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            written += read;
            if (written > declared)
            {
                throw ArchiveException.For(ArchiveFailure.TooLarge, archivePath, memberPath, $"member '{memberPath}' yields more than the {declared} bytes its header declares");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        // The tree is moved into place and reused for good, so its files must be on the disk first.
        RandomAccess.FlushToDisk(output.SafeFileHandle);
    }

    // Rented on the first write.
    private byte[] Buffer => _buffer ??= ArrayPool<byte>.Shared.Rent(ChunkSize);

    public void Dispose()
    {
        if (_buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }
    }
}
