using System.Text;

namespace Autopilot.Core.Net;

/// <summary>Reads an answer's body under its size limit and the attempt's stall timer.</summary>
internal static class Bodies
{
    private const int ChunkSize = 81920;

    public static void ThrowIfTooLarge(long? size, long maxBytes, string host)
    {
        if (size > maxBytes)
        {
            throw new NetException(NetErrorKind.TooLarge, host, $"{host}: {size} bytes is over the limit of {maxBytes}");
        }
    }

    /// <summary>
    /// Copies the body, restarting the stall timer on every chunk and refusing it before a byte
    /// past the limit is written. A body shorter than its declared length was cut.
    /// </summary>
    public static async Task<long> CopyAsync(
        Exchange exchange,
        Stream destination,
        Transfer transfer,
        long maxBytes,
        IProgress<DownloadProgress>? progress,
        Attempt attempt)
    {
        await using Stream source = await exchange.Response.Content.ReadAsStreamAsync(attempt.Token);
        byte[] buffer = new byte[ChunkSize];
        long received = 0;
        for (int read; (read = await ReadAsync(source, buffer, exchange.Host, attempt)) > 0;)
        {
            ThrowIfTooLarge(transfer.Done + read, maxBytes, exchange.Host);
            await destination.WriteAsync(buffer.AsMemory(0, read), attempt.Token);
            received += read;
            transfer.Done += read;
            progress?.Report(new(transfer.Done, transfer.Total));
        }

        long? declared = exchange.Response.Content.Headers.ContentLength;
        return received < declared
            ? throw new NetException(NetErrorKind.Cut, exchange.Host, $"{exchange.Host}: body ended after {received} of {declared} bytes") { IsRetryable = true }
            : transfer.Done;
    }

    /// <summary>A whole text body, decoded as UTF-8 without its byte order mark, refused when declared or found over the limit.</summary>
    public static async Task<string> ReadTextAsync(Exchange exchange, long maxBytes, Attempt attempt)
    {
        long? declared = exchange.Response.Content.Headers.ContentLength;
        ThrowIfTooLarge(declared, maxBytes, exchange.Host);
        using MemoryStream body = new();
        _ = await CopyAsync(exchange, body, new() { Total = declared }, maxBytes, null, attempt);
        ReadOnlySpan<byte> bytes = body.GetBuffer().AsSpan(0, (int)body.Length);
        return Encoding.UTF8.GetString(bytes.StartsWith(Encoding.UTF8.Preamble) ? bytes[Encoding.UTF8.Preamble.Length..] : bytes);
    }

    // Only a failed read is a network failure: a failed write (a full disk) is the caller's.
    private static async Task<int> ReadAsync(Stream source, byte[] buffer, string host, Attempt attempt)
    {
        attempt.Restart();
        try
        {
            return await source.ReadAsync(buffer, attempt.Token);
        }
        catch (IOException failure)
        {
            throw NetFailures.FromTransport(failure, host);
        }
    }
}
