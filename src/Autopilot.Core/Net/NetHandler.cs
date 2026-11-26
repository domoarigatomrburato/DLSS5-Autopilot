using System.Net;

namespace Autopilot.Core.Net;

/// <summary>The production transport behind <see cref="NetClient"/>.</summary>
public static class NetHandler
{
    /// <summary>
    /// Redirects are left to the client, which checks every hop; certificates are left to the
    /// system store, so nothing here touches TLS validation.
    /// </summary>
    public static SocketsHttpHandler Create(NetOptions options) =>
        new()
        {
            AllowAutoRedirect = false,
            ConnectTimeout = options.ConnectTimeout,
            // Bytes counted against a limit and offsets resumed with Range are the file's own.
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
        };
}
