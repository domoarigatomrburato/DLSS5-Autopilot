using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Graphics;
using Autopilot.Core.Resources;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Install.Plans;

/// <summary>
/// The builds a gather takes from the mirror's own families: the <c>renodx-dlss5</c> add-on and the card's <c>nvngx_dlssnr.dll</c>.
/// </summary>
/// <param name="fetch">Fetches and records each build.</param>
/// <param name="request">The route, the game and the card.</param>
/// <param name="read">The settings as the route reads them.</param>
/// <param name="cancellation">The gather's token.</param>
internal sealed class MirrorGather(BuildFetch fetch, GatherRequest request, RouteSettings read, CancellationToken cancellation)
{
    private GatheredFiles Gathered => fetch.Gathered;

    /// <summary>The person's own add-on where it is, else the build the pins and the person's label decide; a pin not listed stops the install.</summary>
    public async Task RenodxAsync()
    {
        if (read.RenodxFile is { } own)
        {
            await fetch.AddAsync(ComponentIds.Renodx, KnownFiles.RenodxAddon, own);
            Gathered.Version(ComponentIds.Renodx, PlanGather.OwnFileLabel);
            return;
        }

        BuildCatalog catalog = await fetch.CatalogAsync();
        RenodxDecision decision = RenodxPins.Decide(new(read.RenodxLabel, false, request.Facts.Gpu?.Driver, request.Facts.Api, null));
        PickedBuild picked = RenodxPins.Pick(catalog, decision);
        Gathered.Note(picked.WantedMissing ? Texts.Format("PlanNoteWantedMissing", read.RenodxLabel, KnownFiles.RenodxAddon, picked.Entry.Label) : decision.Evidence);
        await fetch.AddBuildAsync(picked.Entry, catalog, KnownFiles.RenodxAddon, await fetch.FetchAsync(picked.Entry, catalog, KnownFiles.RenodxAddon));
    }

    /// <summary>
    /// The card's build (gpu.md), each candidate read and checked before one is chosen; none usable stops the install. On
    /// Auto, a pinned build whose architectures exclude the card is skipped before it is downloaded, unless the person
    /// places a mismatch.
    /// </summary>
    public async Task DlssnrAsync()
    {
        BuildCatalog catalog = await fetch.CatalogAsync();
        DlssnrCandidates candidates = DlssnrBuilds.Candidates(catalog, request.Facts.Gpu?.Card?.Sm, read.DlssnrLabel);
        Dictionary<BuildEntry, string> paths = [];
        DlssnrChosen chosen = await ChooseAsync(candidates, catalog, paths);
        await RecordAsync(chosen, catalog, candidates.WantedMissing, paths[chosen.Entry]);
    }

    // The choice among the candidates, each fetched one's path kept in paths; a refusal stops the install with its own message.
    private async Task<DlssnrChosen> ChooseAsync(DlssnrCandidates candidates, BuildCatalog catalog, Dictionary<BuildEntry, string> paths)
    {
        bool skipExcluded = !candidates.Pinned && !request.PlaceMismatch;
        DlssnrOutcome outcome = await DlssnrBuilds.ChooseAsync(
            candidates,
            request.Facts.Gpu?.Card,
            request.PlaceMismatch,
            (entry, _) => ArchitecturesAsync(entry, catalog, skipExcluded, paths),
            cancellation);
        return outcome as DlssnrChosen ?? throw new ComponentException(ComponentFailure.NoUsableBuild, ((DlssnrRefused)outcome).Message);
    }

    // A candidate's architectures: a pinned build's from its pin when the pin excludes the card (no download), else read from
    // the file fetched and checked, its path kept.
    private async Task<IReadOnlySet<int>> ArchitecturesAsync(BuildEntry entry, BuildCatalog catalog, bool skipExcluded, Dictionary<BuildEntry, string> paths)
    {
        if (skipExcluded && DlssnrPins.Of(entry.Label) is { } pin && request.Facts.Gpu?.Card?.Sm is { } sm && !pin.Sm.Contains(sm))
        {
            return pin.Sm.ToHashSet();
        }

        string path = await fetch.FetchAsync(entry, catalog, KnownFiles.NeuralRendering);
        paths[entry] = path;
        return Fatbin.Architectures(path);
    }

    // The chosen build, and what the choice says: a label not listed, the builds skipped, a warning.
    private async Task RecordAsync(DlssnrChosen chosen, BuildCatalog catalog, bool wantedMissing, string path)
    {
        Gathered.Note(wantedMissing ? Texts.Format("PlanNoteWantedMissing", read.DlssnrLabel, KnownFiles.NeuralRendering, chosen.Entry.Label) : null);
        Gathered.Note(chosen.SkippedNote);
        Gathered.Note(chosen.UntrustedNote);
        Gathered.Warn(chosen.Warning);
        await fetch.AddBuildAsync(chosen.Entry, catalog, KnownFiles.NeuralRendering, path);
    }
}
