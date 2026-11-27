using Autopilot.Core.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components;

/// <summary>
/// The builds of <c>RankFTW/rhi-repo</c>, the mirror that carries the <c>nvngx_dlssnr.dll</c> and RenoDX
/// builds, grouped into families, with NVIDIA's own runtimes put in front. The mirror's list comes
/// from the API's release list (one page of 100, with each pinned build it no longer reaches asked
/// for by its tag), else github.com's pages, shortened to three builds per family, always keeping
/// the builds the tool pins. The list is remembered for the process, except the shortened one and
/// one that lacks a pin the network kept from it: the next install asks the API again.
/// </summary>
public sealed class MirrorCatalog(GitHubReleases releases, NvidiaRuntimes nvidia, ILogger<MirrorCatalog> logger)
{
    /// <summary>The mirror's repository.</summary>
    public const string Repository = "RankFTW/rhi-repo";

    private readonly MirrorPins _pins = new(releases, logger);
    private readonly ResolutionMemory<bool, MirrorList> _memory = new(list => list.Complete);

    /// <summary>Every family's builds, newest first, NVIDIA's runtimes in front of the mirror's.</summary>
    /// <exception cref="NetException">Neither the API, nor a kept answer, nor the pages could answer, or the API does not know the repository.</exception>
    /// <exception cref="ReleaseDataException">The API's answer is not release data.</exception>
    public async Task<BuildCatalog> GetAsync(CancellationToken cancellationToken)
    {
        MirrorList mirror = await _memory.GetAsync(true, ReadAsync, cancellationToken);
        IReadOnlyList<BuildEntry> runtimes = await nvidia.GetAsync(cancellationToken);
        Dictionary<BuildFamily, IReadOnlyList<BuildEntry>> families = [with(mirror.Families)];
        foreach (IGrouping<BuildFamily, BuildEntry> own in runtimes.GroupBy(entry => entry.Family))
        {
            families[own.Key] = [.. own, .. families.GetValueOrDefault(own.Key) ?? []];
        }

        return new(families, mirror.Source);
    }

    private async Task<MirrorList> ReadAsync(CancellationToken cancellationToken)
    {
        ReleaseListAnswer list = await releases.GetListAsync(
            Repository,
            GitHubReleases.MaxListCount,
            (pages, repo, token) => new MirrorPages(pages, repo).ReadAsync(token),
            cancellationToken);
        if (list.Source.Origin == ReleaseOrigin.Pages)
        {
            return new(MirrorReleases.Group(list.Releases), list.Source, Complete: false);
        }

        (IReadOnlyList<GitHubRelease> all, bool complete) = await _pins.AddMissingAsync(list.Releases, cancellationToken);
        return new(MirrorReleases.Group(all), list.Source, complete);
    }

    // Complete: read from the API with every pin the network could be asked for, so kept for the run.
    private sealed record MirrorList(Dictionary<BuildFamily, IReadOnlyList<BuildEntry>> Families, ReleaseSource Source, bool Complete);
}
