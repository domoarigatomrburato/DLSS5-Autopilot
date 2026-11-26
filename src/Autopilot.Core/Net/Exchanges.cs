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

    // GitHub's guidance for a secondary rate limit that names no time.
    private static readonly TimeSpan _apiRefusalWait = TimeSpan.FromSeconds(60);

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
                ThrowIfApiRefused(current, response, _gitHub.Note(current, response));
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
        long offset = exchange.StartOffset(transfer);
        HttpContentHeaders headers = exchange.Response.Content.Headers;
        transfer.Total = headers.ContentRange?.Length ?? (offset + headers.ContentLength);
        Bodies.ThrowIfTooLarge(transfer.Total, maxBytes, exchange.Host);
        transfer.Done = offset;
        await using Stream destination = openDestination(offset);
        return await Bodies.CopyAsync(exchange, destination, transfer, maxBytes, progress, attempt);
    }

    /// <summary>A refusal for asking too often, until <paramref name="retryAt"/> when known.</summary>
    public static NetException RateLimited(string host, DateTimeOffset? retryAt, HttpStatusCode? status)
    {
        string because = status is { } code ? $", HTTP {(int)code}" : "";
        string until = retryAt is { } at ? $" until {at:O}" : "";
        return new(NetErrorKind.RateLimited, host, $"{host}: rate limited{because}{until}")
        {
            Detail = new RateLimitedDetail(retryAt, status),
        };
    }

    /// <summary>The wait a server asked for, in seconds or as a date.</summary>
    private static TimeSpan? RetryAfter(HttpResponseMessage response, DateTimeOffset now) =>
        response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date > now ? date - now : TimeSpan.Zero,
            _ => null,
        };

    /// <summary>A 5xx or a 408 is a hiccup worth another attempt, unless the server asks for a long wait.</summary>
    private void ThrowIfRetryableStatus(Exchange exchange)
    {
        HttpStatusCode status = exchange.Response.StatusCode;
        if ((int)status < 500 && status != HttpStatusCode.RequestTimeout)
        {
            return;
        }

        DateTimeOffset now = time.GetUtcNow();
        TimeSpan? retryAfter = RetryAfter(exchange.Response, now);
        if (retryAfter > _longestRetryAfter)
        {
            throw RateLimited(exchange.Host, now + retryAfter, status);
        }

        throw exchange.StatusFailure(retryable: true, retryAfter);
    }

    /// <summary>
    /// A 403 or 429 from the API is a rate limit: it holds the API back until the server's
    /// <c>Retry-After</c>, else until the reset of an allowance that is used up, else for a minute.
    /// </summary>
    private void ThrowIfApiRefused(Uri url, HttpResponseMessage response, DateTimeOffset? exhaustedUntil)
    {
        if (!GitHubRateLimit.IsApi(url) || response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests))
        {
            return;
        }

        DateTimeOffset now = time.GetUtcNow();
        DateTimeOffset retryAt = RetryAfter(response, now) is { } wait ? now + wait : exhaustedUntil ?? (now + _apiRefusalWait);
        _gitHub.Block(retryAt);
        throw RateLimited(GitHubRateLimit.ApiHost, retryAt, response.StatusCode);
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
        return problem is not { } refused
            ? next
            : throw new NetException(NetErrorKind.RedirectRefused, next.Host, $"redirect hop {hop} to {next.Scheme}://{next.Host}{next.AbsolutePath} refused: {refused}")
            {
                Detail = new RedirectRefusedDetail(refused, hop),
            };
    }
}
