namespace Autopilot.Core.Net;

/// <summary>The network client's limits. Every value is injectable, so tests never wait on a real clock.</summary>
/// <param name="Version">The running version, sent in the user agent.</param>
public sealed record NetOptions(string Version)
{
    public string UserAgent => $"invidia-claritatis/{Version}";

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>No bytes for this long, while waiting for the headers or reading the body, drops the attempt.</summary>
    public TimeSpan StallTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan TextTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>A backstop only: the stall timeout catches a dead transfer.</summary>
    public TimeSpan FileTimeout { get; init; } = TimeSpan.FromHours(2);

    /// <summary>The waits between attempts; there is one attempt more than there are waits.</summary>
    public IReadOnlyList<TimeSpan> RetryWaits { get; init; } = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4)];

    public int MaxRedirects { get; init; } = 5;

    public TimeSpan ApiFreshFor { get; init; } = TimeSpan.FromHours(6);

    public long MaxTextBytes { get; init; } = 32L << 20;

    /// <summary>The limit of a download whose caller names none.</summary>
    public long MaxFileBytes { get; init; } = 1L << 30;

    /// <summary>The most a caller may allow one download: GitHub's release asset cap, which RTX Remix mods need.</summary>
    public long MaxFileBytesCeiling { get; init; } = 2L << 30;
}
