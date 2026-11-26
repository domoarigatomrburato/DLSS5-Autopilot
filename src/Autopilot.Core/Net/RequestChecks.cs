namespace Autopilot.Core.Net;

/// <summary>What is checked about a request before anything is sent.</summary>
internal static class RequestChecks
{
    /// <summary>Only https, and only to a host on the caller's list.</summary>
    public static void Check(Uri url, HostAllowlist hosts)
    {
        if (!url.IsAbsoluteUri)
        {
            throw new ArgumentException("the URL must be absolute", nameof(url));
        }

        if (url.Scheme != Uri.UriSchemeHttps)
        {
            throw new NetException(NetErrorKind.SchemeRefused, url.Host, $"{url.Scheme}: URLs are refused, only https is opened");
        }

        if (!hosts.Allows(url.Host))
        {
            throw new NetException(NetErrorKind.HostNotAllowed, url.Host, $"{url.Host} is not on this request's host list");
        }
    }

    /// <summary>The caller's value, or the default; above the ceiling is a programming error.</summary>
    public static T Limit<T>(T? requested, T fallback, T ceiling, string name)
        where T : struct, IComparable<T>
    {
        T value = requested ?? fallback;
        return value.CompareTo(ceiling) > 0 || value.CompareTo(default) <= 0
            ? throw new ArgumentOutOfRangeException(name, value, $"must be above zero and at most {ceiling}")
            : value;
    }
}
