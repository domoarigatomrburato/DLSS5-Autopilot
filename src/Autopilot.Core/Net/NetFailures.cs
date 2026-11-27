using System.Collections.Frozen;
using System.Net.Sockets;
using System.Security.Authentication;

namespace Autopilot.Core.Net;

/// <summary>Turns a transport failure into the kind a person is told about: by code first, by text only as a fallback.</summary>
internal static class NetFailures
{
    private static readonly FrozenDictionary<SocketError, NetErrorKind> _bySocketCode = new Dictionary<SocketError, NetErrorKind>
    {
        [SocketError.AccessDenied] = NetErrorKind.Blocked,
        [SocketError.ConnectionRefused] = NetErrorKind.Refused,
        [SocketError.TimedOut] = NetErrorKind.Timeout,
        [SocketError.HostNotFound] = NetErrorKind.Dns,
        [SocketError.TryAgain] = NetErrorKind.Dns,
        [SocketError.NoData] = NetErrorKind.Dns,
        [SocketError.ConnectionReset] = NetErrorKind.Cut,
        [SocketError.ConnectionAborted] = NetErrorKind.Cut,
        [SocketError.Shutdown] = NetErrorKind.Cut,
    }.ToFrozenDictionary();

    // Windows error text is localised, so only the WinError numbers it carries and the English
    // words other systems use are matched. First match wins; anything else dropped the connection.
    private static readonly (NetErrorKind Kind, string[] Keys)[] _byText =
    [
        (NetErrorKind.Blocked, ["10013"]),
        (NetErrorKind.Dns, ["11001", "11004", "name or service not known", "no such host"]),
        (NetErrorKind.Refused, ["10061", "connection refused"]),
        (NetErrorKind.Timeout, ["10060", "timed out"]),
    ];

    // .NET reports why it refused a certificate only through a validation callback, which this tool
    // never sets. The reason survives in the AuthenticationException's message as the names of the
    // SslPolicyErrors and X509ChainStatusFlags values; those names are not localised.
    private static readonly (string Token, CertificateProblem Problem)[] _certificateTokens =
    [
        ("RemoteCertificateNameMismatch", CertificateProblem.NameMismatch),
        ("NotTimeValid", CertificateProblem.Clock),
        ("UntrustedRoot", CertificateProblem.Interception),
        ("PartialChain", CertificateProblem.MissingRoot),
    ];

    /// <summary>A failure from sending a request or reading its body.</summary>
    public static NetException FromTransport(Exception failure, string host)
    {
        if (ExceptionChain.Find<AuthenticationException>(failure) is { } refused)
        {
            CertificateProblem problem = _certificateTokens.FirstOrDefault(t => refused.Message.Contains(t.Token, StringComparison.Ordinal)).Problem;
            return new(NetErrorKind.Certificate, host, $"{host}: certificate refused, {problem} ({refused.Message})", failure)
            {
                Detail = new CertificateRefusedDetail(problem),
            };
        }

        NetErrorKind kind = Classify(failure);
        return new(kind, host, $"{host}: {kind} ({failure.Message})", failure)
        {
            IsRetryable = kind is NetErrorKind.Refused or NetErrorKind.Timeout or NetErrorKind.Cut,
        };
    }

    private static NetErrorKind Classify(Exception failure)
    {
        HttpRequestError error = failure switch
        {
            HttpRequestException request => request.HttpRequestError,
            HttpIOException io => io.HttpRequestError,
            _ => HttpRequestError.Unknown,
        };

        if (error == HttpRequestError.NameResolutionError)
        {
            return NetErrorKind.Dns;
        }

        if (error == HttpRequestError.ResponseEnded)
        {
            return NetErrorKind.Cut;
        }

        if (error is HttpRequestError.InvalidResponse or HttpRequestError.ConfigurationLimitExceeded
            or HttpRequestError.ProxyTunnelError or HttpRequestError.UserAuthenticationError)
        {
            return NetErrorKind.BadResponse;
        }

        return ExceptionChain.Find<SocketException>(failure) is { } socket && _bySocketCode.TryGetValue(socket.SocketErrorCode, out NetErrorKind kind)
            ? kind
            : FromText(failure);
    }

    private static NetErrorKind FromText(Exception failure)
    {
        string text = string.Join(" ", ExceptionChain.Of(failure).Select(e => e.Message)).ToUpperInvariant();
        foreach ((NetErrorKind kind, string[] keys) in _byText)
        {
            if (keys.Any(key => text.Contains(key.ToUpperInvariant(), StringComparison.Ordinal)))
            {
                return kind;
            }
        }

        return NetErrorKind.Cut;
    }
}
