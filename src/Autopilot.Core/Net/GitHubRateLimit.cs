using System.Globalization;

namespace Autopilot.Core.Net;

/// <summary>
/// GitHub's anonymous API allowance, read from every answer of <c>api.github.com</c>. With none
/// left, no API request is sent until the reset time.
/// </summary>
internal sealed class GitHubRateLimit(TimeProvider time)
{
    public const string ApiHost = "api.github.com";

    private readonly Lock _lock = new();
    private DateTimeOffset _blockedUntil = DateTimeOffset.MinValue;

    public static bool IsApi(Uri url) => string.Equals(url.Host, ApiHost, StringComparison.OrdinalIgnoreCase);

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
            throw Exchanges.RateLimited(ApiHost, until, status: null);
        }
    }

    /// <summary>Reads the allowance from an API answer, blocking the API when none is left.</summary>
    /// <returns>The reset time, when the answer says no requests are left.</returns>
    public DateTimeOffset? Note(Uri url, HttpResponseMessage response)
    {
        if (!IsApi(url))
        {
            return null;
        }

        DateTimeOffset? reset = Header(response, "x-ratelimit-reset") is { } epoch ? DateTimeOffset.FromUnixTimeSeconds(epoch) : null;
        if (Header(response, "x-ratelimit-remaining") != 0 || reset is not { } exhaustedUntil)
        {
            return null;
        }

        Block(exhaustedUntil);
        return exhaustedUntil;
    }

    public void Block(DateTimeOffset until)
    {
        lock (_lock)
        {
            if (until > _blockedUntil)
            {
                _blockedUntil = until;
            }
        }
    }

    private static long? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values)
            && long.TryParse(values.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out long value)
            ? value
            : null;
}
