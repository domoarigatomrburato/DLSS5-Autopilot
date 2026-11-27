using System.Net;
using Autopilot.Core.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components;

/// <summary>
/// The order GitHub release data is asked in: the API through the cache (fresh for 6 hours, any
/// age when the live request fails), then github.com's pages. A refusal is never covered.
/// </summary>
internal sealed partial class ReleaseResolver(NetClient client, TimeProvider time, ILogger logger)
{
    /// <summary>An API request: kept in the cache, reused while fresh, used at any age when the live request fails.</summary>
    internal static readonly TextRequest ApiRequest = new() { Cached = true };

    /// <summary>
    /// The API's answer, fresh or kept; the pages when the API failed with nothing kept. A value of
    /// <see langword="null"/> is the API's "not found".
    /// </summary>
    public async Task<(T? Value, ReleaseSource Source)> ResolveAsync<T>(
        Uri api,
        Func<Uri, string, T> read,
        Func<CancellationToken, Task<T?>> fromPages,
        CancellationToken cancellationToken)
        where T : class
    {
        TextAnswer answer;
        try
        {
            answer = await client.GetTextAsync(api, ComponentHosts.Allowlist, ApiRequest, cancellationToken);
        }
        catch (NetException failure) when (!failure.IsRefusal)
        {
            // With the pages silent too, the API's own failure is the one to report.
            if (await TryPagesAsync(api, failure, fromPages, cancellationToken) is not { } value)
            {
                throw;
            }

            return (value, new(ReleaseOrigin.Pages, TimeSpan.Zero));
        }

        return (Read(api, answer, read), SourceOf(api, answer, time.GetUtcNow(), logger));
    }

    /// <summary>The API's value; <see langword="null"/> for its "not found".</summary>
    private static T? Read<T>(Uri api, TextAnswer answer, Func<Uri, string, T> read)
        where T : class =>
        answer.IsSuccess ? read(api, answer.Body)
        : answer.StatusCode == HttpStatusCode.NotFound ? null
        : throw StatusFailure(api, answer.StatusCode);

    /// <summary>Where an API answer came from and how old it is at <paramref name="now"/>; a stale one is said in the log.</summary>
    public static ReleaseSource SourceOf(Uri api, TextAnswer answer, DateTimeOffset now, ILogger logger)
    {
        TimeSpan age = now - answer.FetchedAt;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (answer.IsStale && logger.IsEnabled(LogLevel.Warning))
        {
            LogStale(logger, api.AbsolutePath, age);
        }

        return new(answer.IsStale ? ReleaseOrigin.StaleCache : ReleaseOrigin.Api, age);
    }

    /// <summary>The pages' answer, or <see langword="null"/> when they cannot give one either; a refusal propagates.</summary>
    private async Task<T?> TryPagesAsync<T>(Uri api, NetException apiFailure, Func<CancellationToken, Task<T?>> fromPages, CancellationToken cancellationToken)
        where T : class
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            LogToPages(logger, api.AbsolutePath, apiFailure.Kind);
        }

        return await NetFallback.OrNullAsync(fromPages(cancellationToken), failure =>
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                LogPagesFailed(logger, api.AbsolutePath, failure.Host, failure.Kind);
            }
        });
    }

    public static NetException StatusFailure(Uri api, HttpStatusCode status) =>
        new(NetErrorKind.HttpStatus, api.Host, $"{api.Host}: HTTP {(int)status} for {api.AbsolutePath}")
        {
            Detail = new HttpStatusDetail(status),
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "The API failed for {Path}; using the answer kept {Age} ago")]
    private static partial void LogStale(ILogger logger, string path, TimeSpan age);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The API failed for {Path} ({Kind}) with no answer kept; reading github.com's release pages")]
    private static partial void LogToPages(ILogger logger, string path, NetErrorKind kind);

    [LoggerMessage(Level = LogLevel.Warning, Message = "github.com's release pages failed for {Path} at {Host}: {Kind}")]
    private static partial void LogPagesFailed(ILogger logger, string path, string host, NetErrorKind kind);
}
