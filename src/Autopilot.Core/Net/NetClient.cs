using System.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Net;

/// <summary>
/// The one HTTP path of the tool: https only, every redirect hop checked against the caller's
/// hosts, size limits on every read, timeouts and retries of its own, GitHub's rate limits honoured.
/// </summary>
public sealed class NetClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly NetOptions _options;
    private readonly TimeProvider _time;
    private readonly ApiCache _cache;
    private readonly Exchanges _exchanges;
    private readonly Retrier _retrier;

    public NetClient(HttpMessageHandler handler, NetOptions options, TimeProvider time, string cacheRoot, ILogger<NetClient> logger)
    {
        // Timeouts are the client's own: connect in the handler, stall and overall in the retrier.
        _http = new(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        _options = options;
        _time = time;
        _cache = new(cacheRoot, logger);
        _exchanges = new(_http, options, time);
        _retrier = new(options, time, logger);
    }

    /// <summary>
    /// A text answer. A final non-success status is returned with its body. A cached request reuses
    /// a fresh kept answer, and falls back to a kept answer of any age when the live request fails.
    /// </summary>
    public async Task<TextAnswer> GetTextAsync(Uri url, HostAllowlist hosts, TextRequest? options, CancellationToken cancellationToken)
    {
        options ??= new();
        TimeSpan timeout = RequestChecks.Limit(options.Timeout, _options.TextTimeout, _options.TextTimeout, nameof(options));
        long maxBytes = RequestChecks.Limit(options.MaxBytes, _options.MaxTextBytes, _options.MaxTextBytes, nameof(options));
        RequestChecks.Check(url, hosts);
        if (!options.Cached)
        {
            return await FetchTextAsync(url, hosts, timeout, maxBytes, options.AcceptServerError, cancellationToken);
        }

        CachedText? kept = await _cache.ReadAsync(url, cancellationToken);
        if (kept is not null && IsFresh(kept))
        {
            return new(HttpStatusCode.OK, kept.Body, kept.FetchedAt, IsStale: false);
        }

        TextAnswer answer;
        try
        {
            answer = await FetchTextAsync(url, hosts, timeout, maxBytes, options.AcceptServerError, cancellationToken);
        }
        catch (NetException failure) when (kept is not null && IsNetworkFailure(failure))
        {
            return new(HttpStatusCode.OK, kept.Body, kept.FetchedAt, IsStale: true);
        }

        if ((int)answer.StatusCode is >= 200 and <= 299)
        {
            await _cache.KeepAsync(url, new(url.AbsoluteUri, answer.FetchedAt, answer.Body), cancellationToken);
        }

        return answer;
    }

    /// <summary>
    /// Sends one <c>HEAD</c> and does not follow a redirect: the answer says where it points, made
    /// absolute and checked as a hop would be (https only, a host on <paramref name="hosts"/>). It
    /// has a text request's timeout, retries and rate-limit handling, and is never cached.
    /// </summary>
    public async Task<HeadAnswer> HeadAsync(Uri url, HostAllowlist hosts, CancellationToken cancellationToken)
    {
        RequestChecks.Check(url, hosts);
        return await _retrier.RunAsync(url, _options.TextTimeout, attempt => _exchanges.HeadOnceAsync(url, hosts, attempt), cancellationToken);
    }

    /// <summary>
    /// Streams a download into the stream <paramref name="openDestination"/> opens at an offset: 0
    /// to start, or where a cut attempt stopped when the server resumes from there. The caller owns
    /// the file behind it; each stream is disposed at the end of its attempt.
    /// </summary>
    /// <returns>The size of the finished download.</returns>
    internal async Task<long> StreamToAsync(
        Uri url,
        HostAllowlist hosts,
        Func<long, Stream> openDestination,
        FileRequest? options,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        options ??= new();
        TimeSpan timeout = RequestChecks.Limit(options.Timeout, _options.FileTimeout, _options.FileTimeout, nameof(options));
        long maxBytes = RequestChecks.Limit(options.MaxBytes, _options.MaxFileBytes, _options.MaxFileBytesCeiling, nameof(options));
        RequestChecks.Check(url, hosts);
        Transfer transfer = new();
        long size = await _retrier.RunAsync(
            url,
            timeout,
            attempt => _exchanges.StreamOnceAsync(url, hosts, openDestination, maxBytes, transfer, progress, attempt),
            cancellationToken);
        progress?.Report(new(size, size));
        return size;
    }

    public void Dispose() => _http.Dispose();

    // A refusal of what was asked for is not something an old answer can stand in for.
    private static bool IsNetworkFailure(NetException failure) =>
        failure.Kind is not (NetErrorKind.RedirectRefused or NetErrorKind.HostNotAllowed or NetErrorKind.SchemeRefused or NetErrorKind.TooLarge);

    private bool IsFresh(CachedText kept)
    {
        TimeSpan age = _time.GetUtcNow() - kept.FetchedAt;
        return age >= TimeSpan.Zero && age < _options.ApiFreshFor;
    }

    private Task<TextAnswer> FetchTextAsync(
        Uri url,
        HostAllowlist hosts,
        TimeSpan timeout,
        long maxBytes,
        Func<string, bool>? acceptServerError,
        CancellationToken cancellationToken) =>
        _retrier.RunAsync(url, timeout, attempt => _exchanges.GetTextOnceAsync(url, hosts, maxBytes, acceptServerError, attempt), cancellationToken);
}
