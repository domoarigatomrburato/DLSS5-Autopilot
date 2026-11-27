using System.Text.Json;
using Autopilot.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Net;

/// <summary>API answers kept under <c>&lt;cache&gt;/api/&lt;key&gt;.json</c>, written atomically.</summary>
internal sealed partial class ApiCache(string cacheRoot, ILogger logger)
{
    private static readonly JsonSerializerOptions _json = new()
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    private readonly string _folder = Path.Combine(cacheRoot, "api");

    /// <summary>The kept answer for a URL; a missing or unreadable file is no answer.</summary>
    public async Task<CachedText?> ReadAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            // Another process may replace the file while it is read.
            await using FileStream file = new(PathFor(url), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            CachedText? entry = await JsonSerializer.DeserializeAsync<CachedText>(file, _json, cancellationToken);
            return entry?.Url == url.AbsoluteUri ? entry : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Keeps an answer. A cache that cannot be written only costs the next request, so a failure
    /// is logged and the answer stays good.
    /// </summary>
    public async Task KeepAsync(Uri url, CachedText entry, CancellationToken cancellationToken)
    {
        try
        {
            await WriteAsync(url, entry, cancellationToken);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                // The query can carry tokens, so only the host and path are logged.
                LogNotKept(logger, url.Host, url.AbsolutePath, failure.GetType().Name);
            }
        }
    }

    private async Task WriteAsync(Uri url, CachedText entry, CancellationToken cancellationToken) =>
        await AtomicFile.WriteAsync(PathFor(url), async (file, token) => await JsonSerializer.SerializeAsync(file, entry, _json, token), cancellationToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The answer from {Host} for {Path} could not be kept in the cache ({Reason})")]
    private static partial void LogNotKept(ILogger logger, string host, string path, string reason);

    private string PathFor(Uri url) => Path.Combine(_folder, CacheKeys.Api(url) + ".json");
}

internal sealed record CachedText(string Url, DateTimeOffset FetchedAt, string Body);
