using System.Net;

namespace Autopilot.Core.Net;

/// <summary>
/// A request that failed for a reason the person can act on. <see cref="Exception.Message"/> is a
/// short English line for the log; <see cref="NetExplanations.Explain"/> gives the words a person reads.
/// </summary>
public sealed class NetException(NetErrorKind kind, string host, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public NetErrorKind Kind { get; } = kind;

    /// <summary>The host that was being reached; for a refused redirect, the hop's host.</summary>
    public string Host { get; } = host;

    public CertificateProblem? Certificate { get; init; }

    public RedirectProblem? Redirect { get; init; }

    /// <summary>The number of the refused redirect hop, counting from 1.</summary>
    public int? Hop { get; init; }

    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>When the server will answer again, when it said so.</summary>
    public DateTimeOffset? RetryAt { get; init; }

    /// <summary>A dropped connection or a server hiccup: another attempt may succeed.</summary>
    internal bool IsRetryable { get; init; }

    /// <summary>The server's own wait before the next attempt, replacing the default one.</summary>
    internal TimeSpan? RetryAfter { get; init; }
}
