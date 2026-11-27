namespace Autopilot.Core.Tests.TestBeds;

/// <summary>
/// A file's stream on a drive that fills up: while <c>full</c> answers true (always, when not given),
/// each write passes half its bytes to the file, then throws <see cref="DiskFullPoints.Failure"/>.
/// </summary>
internal sealed class FullDriveStream(Stream file, Func<bool>? full = null, DiskFullError error = DiskFullError.Windows112) : Stream
{
    /// <summary>The error a full drive raises on this host: ENOSPC off Windows, ERROR_DISK_FULL on it.</summary>
    public static DiskFullError HostError => OperatingSystem.IsWindows() ? DiskFullError.Windows112 : DiskFullError.Errno28;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => file.Length;

    public override long Position
    {
        get => file.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => file.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => file.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (full?.Invoke() == false)
        {
            file.Write(buffer);
            return;
        }

        file.Write(buffer[..(buffer.Length / 2)]);
        throw DiskFullPoints.Failure(error);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
}
