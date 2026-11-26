namespace Autopilot.Core.Net;

/// <summary>
/// A request that failed for a reason the person can act on. <see cref="Exception.Message"/> is a
/// short English line for the log; <see cref="NetExplanations.Explain(NetException)"/> gives the words a person reads.
/// </summary>
public sealed class NetException(NetErrorKind kind, string host, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public NetErrorKind Kind { get; } = kind;

    /// <summary>The host that was being reached; for a refused redirect, the hop's host.</summary>
    public string Host { get; } = host;

    /// <summary>What the failure knows beyond its kind, when it knows more.</summary>
    public NetFailureDetail? Detail { get; init; }

    /// <summary>A dropped connection or a server hiccup: another attempt may succeed.</summary>
    internal bool IsRetryable { get; init; }

    /// <summary>The server's own wait before the next attempt, replacing the default one.</summary>
    internal TimeSpan? RetryAfter { get; init; }
}
