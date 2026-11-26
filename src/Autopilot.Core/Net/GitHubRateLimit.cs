using System.Globalization;
using System.Net;

namespace Autopilot.Core.Net;

/// <summary>
/// GitHub's anonymous API allowance, read from every answer of <c>api.github.com</c>. With none
/// left, no API request is sent until the reset time.
/// </summary>
internal sealed class GitHubRateLimit(TimeProvider time)
{
    private const string ApiHost = "api.github.com";

    private readonly Lock _lock = new();
    private DateTimeOffset _blockedUntil = DateTimeOffset.MinValue;

    public void ThrowIfBlocked(Uri url)
    {
        if (!IsApi(url))
        {
            return;
        }

        DateTimeOffset until;
        lock (_lock)
        {
            until = _blockedUntil;
        }

        if (time.GetUtcNow() < until)
        {
            throw Limited(until);
        }
    }

    /// <summary>Reads the allowance from an answer, and throws when the answer is itself a refusal.</summary>
    public void Note(Uri url, HttpResponseMessage response)
    {
        if (!IsApi(url))
        {
            return;
        }

        DateTimeOffset? reset = Header(response, "x-ratelimit-reset") is { } epoch ? DateTimeOffset.FromUnixTimeSeconds(epoch) : null;
        if (Header(response, "x-ratelimit-remaining") == 0 && reset is { } exhaustedUntil)
        {
            Block(exhaustedUntil);
        }

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
        {
            DateTimeOffset? retryAt = RetryAfter(response, time.GetUtcNow()) is { } wait ? time.GetUtcNow() + wait : reset;
            if (retryAt is { } until)
            {
                Block(until);
            }

            throw Limited(retryAt);
        }
    }

    /// <summary>The wait a server asked for, in seconds or as a date.</summary>
    public static TimeSpan? RetryAfter(HttpResponseMessage response, DateTimeOffset now) =>
        response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date > now ? date - now : TimeSpan.Zero,
            _ => null,
        };

    private static bool IsApi(Uri url) => string.Equals(url.Host, ApiHost, StringComparison.OrdinalIgnoreCase);

    private static long? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values)
            && long.TryParse(values.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out long value)
            ? value
            : null;

    private static NetException Limited(DateTimeOffset? until) =>
        new(NetErrorKind.RateLimited, ApiHost, until is { } at ? $"{ApiHost}: rate limited until {at:O}" : $"{ApiHost}: rate limited")
        {
            RetryAt = until,
        };

    private void Block(DateTimeOffset until)
    {
        lock (_lock)
        {
            if (until > _blockedUntil)
            {
                _blockedUntil = until;
            }
        }
    }
}
