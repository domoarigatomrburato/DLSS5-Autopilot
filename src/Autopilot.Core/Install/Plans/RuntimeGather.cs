using Autopilot.Core.Archives;
using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Graphics;
using Autopilot.Core.Net;
using Autopilot.Core.Resources;
using Autopilot.Core.Routes;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install.Plans;

/// <summary>NVIDIA's runtimes a gather takes, NVIDIA's own first, then the mirror's: <c>nvngx_dlss.dll</c> and <c>nvngx_dlssd.dll</c>.</summary>
/// <param name="fetch">Fetches and records each build.</param>
/// <param name="read">The settings as the route reads them.</param>
/// <param name="logger">Where a runtime source that failed before the next one served is logged.</param>
internal sealed partial class RuntimeGather(BuildFetch fetch, RouteSettings read, ILogger logger)
{
    private GatheredFiles Gathered => fetch.Gathered;

    /// <summary>The person's <c>nvngx_dlss.dll</c> build, else NVIDIA's newest; when no source can serve it, the install stops.</summary>
    public async Task DlssAsync()
    {
        if (await RuntimeAsync(BuildFamily.Dlss, read.DlssLabel) is { } why)
        {
            throw new ComponentException(ComponentFailure.NoUsableBuild, Texts.Format("PlanRuntimeFailed", KnownFiles.Dlss, why));
        }
    }

    /// <summary>
    /// The ray-reconstruction build; one that cannot be fetched is a skipped line, not a failure (routes.md), and its reason
    /// is kept for the plan's warning, which depends on what the previous install swapped.
    /// </summary>
    public async Task RayReconstructionAsync()
    {
        if (await RuntimeAsync(BuildFamily.Dlssd, read.RayReconstructionLabel) is { } why)
        {
            Gathered.Fail(ComponentIds.Dlssd, why);
            Gathered.Skip(Texts.Format("PlanSkippedRuntimeFailed", KnownFiles.RayReconstruction));
        }
    }

    // components.md, "Placing a runtime from a family": the chosen entry, then the next one; null once one served, else
    // every source's reason.
    private async Task<string?> RuntimeAsync(BuildFamily family, string? label)
    {
        string file = MirrorFamilies.NameOf(family);
        List<string> reasons = [];
        try
        {
            BuildCatalog catalog = await fetch.CatalogAsync();
            PickedBuild picked = catalog.Pick(family, label);
            IReadOnlyList<BuildEntry> candidates = catalog.Candidates(picked);
            for (int at = 0; at < candidates.Count; at++)
            {
                BuildEntry entry = candidates[at];
                if (await TryAsync(entry, catalog, file, reasons))
                {
                    Gathered.Note(picked.WantedMissing ? Texts.Format("PlanNoteWantedMissing", label, file, entry.Label) : null);
                    Gathered.Note(entry.Publisher == BuildPublisher.Mirror ? catalog.NvidiaListNote : null);
                    return null;
                }

                if (at + 1 < candidates.Count && logger.IsEnabled(LogLevel.Warning))
                {
                    LogFallback(logger, file, entry.Label, reasons[^1], candidates[at + 1].Label);
                }
            }
        }
        catch (ComponentException unlisted) when (unlisted.Reason == ComponentFailure.BuildListUnavailable)
        {
            reasons.Add(unlisted.Message);
        }

        return string.Join("; ", reasons);
    }

    // One candidate fetched and checked; false, with the reason kept, when it could not be.
    private async Task<bool> TryAsync(BuildEntry entry, BuildCatalog catalog, string file, List<string> reasons)
    {
        try
        {
            await fetch.AddBuildAsync(entry, catalog, file, await fetch.FetchAsync(entry, catalog, file));
            return true;
        }
        catch (Exception failure) when (failure is ComponentException or NetException or ArchiveException)
        {
            reasons.Add($"{entry.Label}: {failure.Message}");
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{File} {Label} could not be used ({Reason}); trying {Next}")]
    private static partial void LogFallback(ILogger logger, string file, string label, string reason, string next);
}
