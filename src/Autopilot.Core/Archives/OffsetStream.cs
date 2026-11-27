namespace Autopilot.Core.Archives;

/// <summary>
/// A read-only, seekable view of a file from an offset to its end, which a reader takes for the
/// whole file: a zip appended to a program counts its offsets from its own start. .NET's
/// <c>System.IO.SubReadStream</c> and SharpCompress 0.50.12's <c>ReadOnlySubStream</c>,
/// <c>BufferedSubStream</c> and <c>TarReadOnlySubStream</c> are all internal.
/// </summary>
internal sealed class OffsetStream : Stream
{
    private readonly FileStream _file;
    private readonly long _start;

    public OffsetStream(FileStream file, long start)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, file.Length);
        _file = file;
        _start = start;
        _file.Position = start;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _file.Length - _start;

    public override long Position
    {
        get => _file.Position - _start;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _file.Position = _start + value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => _file.Read(buffer, offset, count);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _file.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        return Position;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _file.Dispose();
        }
        base.Dispose(disposing);
    }
}
