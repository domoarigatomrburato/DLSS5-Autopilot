using System.Collections.Frozen;
using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Fetch;
using Autopilot.Core.Net;
using Autopilot.Core.Routes;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install.Plans;

/// <summary>What a gather fetches for: the route, the game and the card, the settings, and the person's override.</summary>
/// <param name="Route">The chosen route.</param>
/// <param name="Facts">The game and the card.</param>
/// <param name="Settings">The person's settings; only those the route shows are read.</param>
internal sealed record GatherRequest(RouteId Route, RouteFacts Facts, RouteSettings Settings)
{
    /// <summary>"Install anyway": an <c>nvngx_dlssnr.dll</c> build that does not match the card is placed (a one-off).</summary>
    public bool PlaceMismatch { get; init; }
}

/// <summary>
/// Phase 2 of an install (install.md, "Order of an install"): every component the route installs resolved, downloaded,
/// verified and unpacked into the download cache, and nothing else written; each placed file's facts taken once. The
/// person's choices pick the builds; a ray-reconstruction runtime that cannot be fetched is a skipped line, not a failure.
/// </summary>
/// <param name="run">The job's view of the sources.</param>
/// <param name="files">Fetches a resolved component or a build into the cache, checked.</param>
/// <param name="logger">Where a runtime source that failed before the next one served is logged.</param>
internal sealed class PlanGather(ComponentRun run, ComponentFiles files, ILogger logger)
{
    /// <summary>The label recorded for the person's own <c>renodx-dlss5</c> add-on.</summary>
    internal const string OwnFileLabel = "own file";

    // The ids gathered their own way; any other the route needs is gathered as its source resolves it.
    private static readonly FrozenDictionary<string, Func<Gathers, Task>> _special = new Dictionary<string, Func<Gathers, Task>>(StringComparer.Ordinal)
    {
        [ComponentIds.ReShade] = gathers => gathers.Releases.ReShadeAsync(),
        [ComponentIds.Renodx] = gathers => gathers.Mirror.RenodxAsync(),
        [ComponentIds.Dlssnr] = gathers => gathers.Mirror.DlssnrAsync(),
        [ComponentIds.Dlss] = gathers => gathers.Runtimes.DlssAsync(),
        [ComponentIds.Dlssd] = gathers => gathers.Runtimes.RayReconstructionAsync(),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // The ids gathered as their sources resolve them that the routes written so far need: none yet; g2 and h add theirs.
    private static readonly FrozenSet<string> _generic = [];

    /// <summary>Gathers what <paramref name="request"/>'s route installs.</summary>
    /// <exception cref="NotSupportedException">The route needs a component this gather does not fetch yet; nothing was asked.</exception>
    /// <exception cref="ComponentException">A component could not be resolved, its release changed shape, no build fits, or a pin is missing.</exception>
    /// <exception cref="NetException">A download failed or was refused.</exception>
    /// <exception cref="Archives.ArchiveException">An archive was refused.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public Task<GatheredFiles> GatherAsync(GatherRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<string> ids = Supported(request);
        BuildFetch fetch = new(run.Mirror, files, new GatheredFiles(), progress, cancellationToken);
        return EachAsync(ids, GathersOf(request, fetch, cancellationToken), fetch.Gathered);
    }

    // Each part of the gather, over the settings as the route reads them.
    private Gathers GathersOf(GatherRequest request, BuildFetch fetch, CancellationToken cancellationToken)
    {
        RouteSettings read = ShownSettings.Reset(request.Route, request.Facts, request.Settings);
        return new(new(run, fetch, cancellationToken), new(fetch, request, read, cancellationToken), new(fetch, read, logger));
    }

    // The route's ids, refused whole when one has no gather yet, before anything is asked.
    private static IReadOnlyList<string> Supported(GatherRequest request)
    {
        IReadOnlyList<string> ids = RouteComponents.For(request.Route, request.Facts, request.Settings);
        return ids.FirstOrDefault(id => !_special.ContainsKey(id) && !_generic.Contains(id)) is { } unknown
            ? throw new NotSupportedException($"gathering '{unknown}' is not written yet")
            : ids;
    }

    // Each id in the route's order, so a required file that fails stops the gather before the later ones are fetched.
    private static async Task<GatheredFiles> EachAsync(IReadOnlyList<string> ids, Gathers gathers, GatheredFiles gathered)
    {
        foreach (string id in ids)
        {
            await (_special.TryGetValue(id, out Func<Gathers, Task>? special) ? special(gathers) : gathers.Releases.ComponentAsync(id));
        }

        return gathered;
    }

    private sealed record Gathers(ReleaseGather Releases, MirrorGather Mirror, RuntimeGather Runtimes);
}
