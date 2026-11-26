using System.Net.Sockets;

namespace Autopilot.Core.Tests.Net;

/// <summary>A body that never sends a byte: it waits for cancellation, or fails at once when broken.</summary>
internal sealed class StalledStream(bool broken) : Stream
{
    /// <summary>A task that ends only when the token is cancelled, and then synchronously.</summary>
    public static async Task<T> Never<T>(CancellationToken cancellationToken)
    {
        TaskCompletionSource<T> never = new();
        await using CancellationTokenRegistration registration = cancellationToken.Register(() => never.TrySetCanceled(cancellationToken));
        return await never.Task;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (broken)
        {
            throw new IOException("Unable to read data from the transport connection", new SocketException((int)SocketError.ConnectionReset));
        }

        return await Never<int>(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
