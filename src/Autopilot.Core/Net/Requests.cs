using System.Net;

namespace Autopilot.Core.Net;

/// <summary>What one text request asks for. Limits may only be lowered from <see cref="NetOptions"/>.</summary>
public sealed record TextRequest
{
    /// <summary>An API answer: kept in the cache, reused while fresh, and used at any age when the live request fails.</summary>
    public bool Cached { get; init; }

    public TimeSpan? Timeout { get; init; }

    public long? MaxBytes { get; init; }
}

/// <summary>What one download asks for. Limits may only be lowered from <see cref="NetOptions"/>.</summary>
public sealed record FileRequest
{
    public TimeSpan? Timeout { get; init; }

    public long? MaxBytes { get; init; }
}

/// <summary>A text answer. A stale one came from the cache because the live request failed.</summary>
public sealed record TextAnswer(HttpStatusCode StatusCode, string Body, DateTimeOffset FetchedAt, bool IsStale);

/// <summary>A finished download.</summary>
public sealed record StreamResult(long Size);

/// <summary>Bytes received so far, and the total when the server said it.</summary>
public readonly record struct DownloadProgress(long Done, long? Total);
