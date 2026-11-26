using System.Net;
using System.Net.Http.Headers;

namespace Autopilot.Core.Net;

/// <summary>
/// Sends one GET and follows its redirects, checking every hop before it is asked, then sorts the
/// final status: a hiccup worth another attempt, a rate limit, or the answer.
/// </summary>
internal sealed class Exchanges(HttpClient http, NetOptions options, TimeProvider time)
{
    private static readonly TimeSpan _longestRetryAfter = TimeSpan.FromSeconds(60);

    private readonly GitHubRateLimit _gitHub = new(time);

    public async Task<Exchange> SendAsync(Uri url, HostAllowlist hosts, long resumeFrom, Attempt attempt)
    {
        Uri current = url;
        for (int hop = 1; ; hop++)
        {
            attempt.Url = current;
            _gitHub.ThrowIfBlocked(current);
            HttpResponseMessage response = await SendOnceAsync(current, resumeFrom, attempt);
            Uri? next;
            try
            {
                _gitHub.Note(current, response);
                next = RedirectTarget(response, current);
            }
            catch
            {
                response.Dispose();
                throw;
            }

            if (next is null)
            {
                return new(response, current);
            }

            response.Dispose();
            current = CheckHop(next, hosts, hop);
        }
    }

    /// <summary>One attempt at a text answer: any final status, with its body.</summary>
    public async Task<TextAnswer> GetTextOnceAsync(Uri url, HostAllowlist hosts, long maxBytes, Attempt attempt)
    {
        using Exchange exchange = await SendAsync(url, hosts, 0, attempt);
        ThrowIfRetryableStatus(exchange);
        string text = await Bodies.ReadTextAsync(exchange, maxBytes, attempt);
        return new(exchange.Response.StatusCode, text, time.GetUtcNow(), IsStale: false);
    }

    /// <summary>One attempt at a download, resuming from what earlier attempts wrote when the server allows.</summary>
    public async Task<long> StreamOnceAsync(
        Uri url,
        HostAllowlist hosts,
        Func<long, Stream> openDestination,
        long maxBytes,
        Transfer transfer,
        IProgress<DownloadProgress>? progress,
        Attempt attempt)
    {
        using Exchange exchange = await SendAsync(url, hosts, transfer.Done, attempt);
        ThrowIfRetryableStatus(exchange);
        long offset = exchange.StartOffset(transfer.Done);
        HttpContentHeaders headers = exchange.Response.Content.Headers;
        transfer.Total = headers.ContentRange?.Length ?? (offset + headers.ContentLength);
        Bodies.ThrowIfTooLarge(transfer.Total, maxBytes, exchange.Host);
        transfer.Done = offset;
        await using Stream destination = openDestination(offset);
        return await Bodies.CopyAsync(exchange, destination, transfer, maxBytes, progress, attempt);
    }

    /// <summary>A 5xx or a 408 is a hiccup worth another attempt, unless the server asks for a long wait.</summary>
    public void ThrowIfRetryableStatus(Exchange exchange)
    {
        HttpStatusCode status = exchange.Response.StatusCode;
        if ((int)status < 500 && status != HttpStatusCode.RequestTimeout)
        {
            return;
        }

        DateTimeOffset now = time.GetUtcNow();
        TimeSpan? retryAfter = GitHubRateLimit.RetryAfter(exchange.Response, now);
        if (retryAfter > _longestRetryAfter)
        {
            throw new NetException(NetErrorKind.RateLimited, exchange.Host, $"{exchange.Host}: HTTP {(int)status}, retry after {retryAfter}")
            {
                RetryAt = now + retryAfter,
                StatusCode = status,
            };
        }

        throw exchange.StatusFailure(retryable: true, retryAfter);
    }

    private static Uri? RedirectTarget(HttpResponseMessage response, Uri current) =>
        (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
        && response.Headers.Location is { } location
            ? new Uri(current, location)
            : null;

    private async Task<HttpResponseMessage> SendOnceAsync(Uri url, long resumeFrom, Attempt attempt)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        if (resumeFrom > 0)
        {
            request.Headers.Range = new(resumeFrom, null);
        }

        attempt.Restart();
        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, attempt.Token);
    }

    private Uri CheckHop(Uri next, HostAllowlist hosts, int hop)
    {
        RedirectProblem? problem = hop > options.MaxRedirects ? RedirectProblem.TooManyHops
            : next.Scheme != Uri.UriSchemeHttps ? RedirectProblem.Scheme
            : !hosts.Allows(next.Host) ? RedirectProblem.Host
            : null;
        return problem is null
            ? next
            : throw new NetException(NetErrorKind.RedirectRefused, next.Host, $"redirect hop {hop} to {next.Scheme}://{next.Host}{next.AbsolutePath} refused: {problem}")
            {
                Redirect = problem,
                Hop = hop,
            };
    }
}
