using System.Globalization;
using System.Net;
using Autopilot.Core.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components;

/// <summary>
/// The builds of <c>RankFTW/rhi-repo</c>, the mirror that carries the <c>nvngx_dlssnr.dll</c> and RenoDX
/// builds, grouped into families, with NVIDIA's own runtimes put in front. The mirror's list comes
/// from the API's release list (one page of 100, with each pinned build it no longer reaches asked
/// for by its tag), else github.com's pages, shortened to <see cref="PagesBuildsPerFamily"/> builds
/// per family, always keeping the builds the tool pins. The list is remembered for the process,
/// except the shortened one and one that lacks a pin the network kept from it: the next install asks
/// the API again.
/// </summary>
public sealed class MirrorCatalog(NetClient client, TimeProvider time, GitHubReleases releases, NvidiaRuntimes nvidia, ILogger<MirrorCatalog> logger)
{
    /// <summary>The mirror's repository.</summary>
    public const string Repository = "RankFTW/rhi-repo";

    /// <summary>How many builds per family github.com's pages are read for.</summary>
    public const int PagesBuildsPerFamily = MirrorPages.BuildsPerFamily;

    private static readonly GitHubRepository _repository = GitHubRepository.Parse(Repository);
    private static readonly Uri _api = _repository.Api("?per_page=" + GitHubReleases.MaxListCount.ToString(CultureInfo.InvariantCulture));

    private readonly ReleaseResolver _resolver = new(client, time, logger);
    private readonly MirrorPages _pages = new(new ReleasePages(client, logger), _repository);
    private readonly MirrorPins _pins = new(releases, logger);
    private MirrorList? _remembered;

    /// <summary>Every family's builds, newest first, NVIDIA's runtimes in front of the mirror's.</summary>
    /// <exception cref="NetException">Neither the API, nor a kept answer, nor the pages could answer, or the API does not know the repository.</exception>
    /// <exception cref="ReleaseDataException">The API's answer is not release data.</exception>
    public async Task<BuildCatalog> GetAsync(CancellationToken cancellationToken)
    {
        MirrorList mirror = _remembered ?? await ReadAsync(cancellationToken);
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
        (IReadOnlyList<GitHubRelease>? list, ReleaseSource source) = await _resolver.ResolveAsync(_api, ReleaseJson.List, _pages.ReadAsync, cancellationToken);
        return source.Origin == ReleaseOrigin.Pages
            ? new(MirrorReleases.Group(Found(list)), source)
            : await WithPinsAsync(Found(list), source, cancellationToken);
    }

    // The API's "not found" is a failure: the mirror's repository is gone or renamed.
    private static IReadOnlyList<GitHubRelease> Found(IReadOnlyList<GitHubRelease>? list) =>
        list ?? throw ReleaseResolver.StatusFailure(_api, HttpStatusCode.NotFound);

    private async Task<MirrorList> WithPinsAsync(IReadOnlyList<GitHubRelease> list, ReleaseSource source, CancellationToken cancellationToken)
    {
        (IReadOnlyList<GitHubRelease> all, bool complete) = await _pins.AddMissingAsync(Repository, list, cancellationToken);
        MirrorList read = new(MirrorReleases.Group(all), source);
        if (complete)
        {
            _remembered = read;
        }

        return read;
    }

    private sealed record MirrorList(Dictionary<BuildFamily, IReadOnlyList<BuildEntry>> Families, ReleaseSource Source);
}
