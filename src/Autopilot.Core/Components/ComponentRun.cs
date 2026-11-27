using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Components.Sources;
using Autopilot.Core.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components;

/// <summary>
/// One job's view of where components come from (an install, an uninstall, "update all", an
/// autopilot pass): every source built fresh, so the run owns every memory of this area (the
/// mirror catalog, NVIDIA's runtime list, each component's latest tag, release list and release
/// page). Two installs in one job share one answer; the next job asks again. The API cache on disk
/// belongs to the <see cref="NetClient"/> and is shared across runs.
/// </summary>
public sealed class ComponentRun
{
    /// <summary>Builds a run from the process-wide pieces; every shared resolution runs under <paramref name="cancellationToken"/>, the job's.</summary>
    public ComponentRun(NetClient client, DownloadCache cache, TimeProvider time, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loggers);
        Releases = new(client, time, loggers.CreateLogger<GitHubReleases>(), cancellationToken);
        Nvidia = new(Releases, loggers.CreateLogger<NvidiaRuntimes>(), cancellationToken);
        Mirror = new(Releases, Nvidia, loggers.CreateLogger<MirrorCatalog>(), cancellationToken);
        Sources = new(Releases, client, time, loggers.CreateLogger<ComponentSources>(), cancellationToken);
        ReShade = new(client, cache, loggers.CreateLogger<ReShadeSource>(), cancellationToken);
        Feeder = new(Releases, loggers.CreateLogger<FeederSource>(), cancellationToken);
        OptiScaler = new(Releases, cache, loggers.CreateLogger<OptiScalerSource>(), cancellationToken);
    }

    /// <summary>GitHub's releases, each list read once per run.</summary>
    public GitHubReleases Releases { get; }

    /// <summary>NVIDIA's own DLSS runtimes.</summary>
    public NvidiaRuntimes Nvidia { get; }

    /// <summary>The mirror's builds by family, NVIDIA's in front.</summary>
    public MirrorCatalog Mirror { get; }

    /// <summary>The components resolved by their catalog rule.</summary>
    public ComponentSources Sources { get; }

    /// <summary>The ReShade add-on installer.</summary>
    public ReShadeSource ReShade { get; }

    /// <summary>The DLSS5-Feeder builds.</summary>
    public FeederSource Feeder { get; }

    /// <summary>The OptiScaler builds.</summary>
    public OptiScalerSource OptiScaler { get; }
}
