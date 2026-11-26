using System.Net;

namespace Autopilot.Core.Net;

/// <summary>
/// One attempt at a request: its cancellation joins the caller's token, the overall limit and a
/// stall timer that restarts whenever bytes arrive.
/// </summary>
internal sealed class Attempt : IDisposable
{
    private readonly CancellationTokenSource _stall;
    private readonly CancellationTokenSource _linked;
    private readonly TimeSpan _stallTimeout;

    public Attempt(Uri url, TimeProvider time, TimeSpan stallTimeout, CancellationToken call)
    {
        Url = url;
        _stallTimeout = stallTimeout;
        _stall = new(stallTimeout, time);
        _linked = CancellationTokenSource.CreateLinkedTokenSource(call, _stall.Token);
    }

    /// <summary>The URL being asked right now; a redirect moves it.</summary>
    public Uri Url { get; set; }

    public CancellationToken Token => _linked.Token;

    public bool Stalled => _stall.IsCancellationRequested;

    public void Restart() => _stall.CancelAfter(_stallTimeout);

    public void Dispose()
    {
        _linked.Dispose();
        _stall.Dispose();
    }
}

/// <summary>A final answer and the URL it came from, after redirects.</summary>
internal sealed class Exchange(HttpResponseMessage response, Uri url) : IDisposable
{
    public HttpResponseMessage Response { get; } = response;

    public string Host { get; } = url.Host;

    public NetException StatusFailure(bool retryable, TimeSpan? retryAfter) =>
        new(NetErrorKind.HttpStatus, Host, $"{Host}: HTTP {(int)Response.StatusCode}")
        {
            Detail = new HttpStatusDetail(Response.StatusCode),
            IsRetryable = retryable,
            RetryAfter = retryAfter,
        };

    /// <summary>
    /// Where the body starts: the resume offset for a matching 206, 0 for any other success. A
    /// resume the server refuses (a 416, or a 206 from another offset) drops what was written, and
    /// the next attempt starts over.
    /// </summary>
    public long StartOffset(Transfer transfer)
    {
        long done = transfer.Done;
        bool matching = Response.Content.Headers.ContentRange?.From == done;
        if (done > 0 && (Response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable
            || (Response.StatusCode == HttpStatusCode.PartialContent && !matching)))
        {
            transfer.Done = 0;
            throw StatusFailure(retryable: true, retryAfter: null);
        }

        if (Response.StatusCode == HttpStatusCode.PartialContent)
        {
            return matching ? done : throw StatusFailure(retryable: false, retryAfter: null);
        }

        return Response.IsSuccessStatusCode ? 0 : throw StatusFailure(retryable: false, retryAfter: null);
    }

    public void Dispose() => Response.Dispose();
}

/// <summary>How far a download got, kept across attempts so a retry can resume.</summary>
internal sealed class Transfer
{
    public long Done { get; set; }

    public long? Total { get; set; }
}
