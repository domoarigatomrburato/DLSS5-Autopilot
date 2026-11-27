using Autopilot.Core.Components;
using Autopilot.Core.Net;
using Autopilot.Core.Tests.Net;
using Microsoft.Extensions.Logging.Testing;

namespace Autopilot.Core.Tests.TestBeds;

/// <summary>
/// Component runs on a network <see cref="TestBed"/>, as jobs build them: one download cache and one
/// logger per source shared by every run, and a fresh <see cref="ComponentRun"/> each time.
/// </summary>
internal sealed class Runs(TestBed net)
{
    public DownloadCache Cache { get; } = new(net.Client, net.CacheRoot, new FakeSigner(), net.Time, new FakeLogger<DownloadCache>());

    /// <summary>Every run's loggers, one per source.</summary>
    public RunLoggers Loggers { get; } = new();

    /// <summary>A new run under <paramref name="cancellation"/>, the job's token: nothing remembered, the network's cache kept.</summary>
    public ComponentRun Next(CancellationToken cancellation) => new(net.Client, Cache, net.Time, Loggers, cancellation);
}
