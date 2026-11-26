namespace Autopilot.Core.Tests.Net;

/// <summary>A slow but live body: one byte after each gap on the fake clock.</summary>
internal sealed class TrickleStream(TimeProvider time, int bytes, TimeSpan gap) : Stream
{
    private int _sent;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => bytes;

    public override long Position { get => _sent; set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_sent == bytes)
        {
            return 0;
        }

        await Task.Delay(gap, time, cancellationToken);
        buffer.Span[0] = (byte)_sent++;
        return 1;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
