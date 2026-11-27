using System.Net;

namespace Autopilot.Core.Net;

/// <summary>What one text request asks for. Limits may only be lowered from <see cref="NetOptions"/>.</summary>
public sealed record TextRequest
{
    /// <summary>An API answer: kept in the cache, reused while fresh, and used at any age when the live request fails.</summary>
    public bool Cached { get; init; }

    public TimeSpan? Timeout { get; init; }

    public long? MaxBytes { get; init; }

    /// <summary>
    /// Whether a 5xx answer is still the answer, judged on its body (read within the size limit):
    /// true returns it at once with its status, false retries it as any other 5xx, and so does a body
    /// past the limit, which is never asked about. Unset, every 5xx is retried. An accepted 5xx is
    /// never kept in the cache.
    /// </summary>
    public Func<string, bool>? AcceptServerError { get; init; }
}

/// <summary>What one download asks for. Limits may only be lowered from <see cref="NetOptions"/>.</summary>
public sealed record FileRequest
{
    public TimeSpan? Timeout { get; init; }

    public long? MaxBytes { get; init; }
}

/// <summary>A text answer. A stale one came from the cache because the live request failed.</summary>
public sealed record TextAnswer(HttpStatusCode StatusCode, string Body, DateTimeOffset FetchedAt, bool IsStale)
{
    /// <summary>Whether the status is a success (2xx).</summary>
    public bool IsSuccess => (int)StatusCode is >= 200 and <= 299;
}

/// <summary>
/// The answer to a <c>HEAD</c>, which is never followed: for a redirect, where it points, made
/// absolute and checked as a hop would be; <see langword="null"/> otherwise.
/// </summary>
public sealed record HeadAnswer(HttpStatusCode StatusCode, Uri? Location);

/// <summary>Bytes received so far, and the total when the server said it.</summary>
public readonly record struct DownloadProgress(long Done, long? Total);
