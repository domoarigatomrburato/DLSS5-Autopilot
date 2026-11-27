using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;

namespace Autopilot.Core.Components.Sources;

/// <summary>
/// kibblerz's standalone-dlssnr add-on from its newest release, in the three shapes it has had: three
/// loose files, a <c>-64-bit.zip</c> holding them, or (with no release data) the archive named after
/// the tag, else the loose files by GitHub's <c>latest/download</c> redirect.
/// </summary>
internal sealed class StandaloneComponent(GitHubReleases releases, NetClient client)
{
    /// <summary>The files of the standalone add-on, published loose before 2.1.0 and inside the <c>-64-bit.zip</c> from then.</summary>
    public static readonly string[] StandaloneFiles = ["standalone-dlssnr.addon64", "nvngx.dll", "DLSS5_AIO_Feed.fx"];

    /// <summary>The standalone add-on's second shader, which later releases carry.</summary>
    public const string StandaloneBoundary = "StandaloneBoundary.fx";

    private static readonly ExpectedFile[] _standaloneLayout =
        [.. StandaloneFiles.Select(name => new ExpectedFile(name)), new(StandaloneBoundary, Optional: true)];

    /// <summary>
    /// The three loose files; else the <c>-64-bit.zip</c>. With no release data: the archive named
    /// after the tag GitHub's redirect gives, else the loose files by the <c>latest/download</c> redirect.
    /// </summary>
    public async Task<ComponentRelease> ResolveAsync(CancellationToken cancellationToken) =>
        await ReleaseAssets.LatestOrNoneAsync(releases, ComponentIds.Standalone, cancellationToken) is { } latest
            ? FromRelease(latest)
            : await ArchiveByNameAsync(cancellationToken) ?? ReleaseAssets.ByLatestRedirect(ComponentIds.Standalone, StandaloneFiles);

    private static ComponentRelease FromRelease(ReleaseAnswer latest)
    {
        IReadOnlyList<GitHubAsset> assets = latest.Release.Assets;
        GitHubAsset[] loose = [.. assets.Where(asset => StandaloneFiles.Contains(asset.Name, StringComparer.OrdinalIgnoreCase))];
        if (loose.Length == StandaloneFiles.Length)
        {
            return ReleaseAssets.Of(ComponentIds.Standalone, latest, [.. loose, .. assets.Where(asset => ReleaseAssets.Is(asset, StandaloneBoundary))], _standaloneLayout);
        }

        GitHubAsset archive = ReleaseAssets.First(
            ComponentIds.Standalone,
            latest.Release,
            asset => asset.Name.EndsWith("-64-bit.zip", StringComparison.OrdinalIgnoreCase),
            string.Join(", ", StandaloneFiles) + " or a -64-bit.zip");
        return ReleaseAssets.Of(ComponentIds.Standalone, latest, [archive], _standaloneLayout);
    }

    // The archive's name is a guess from the tag (the publisher has renamed things before), so it is
    // asked with a HEAD before the install is committed to it.
    private async Task<ComponentRelease?> ArchiveByNameAsync(CancellationToken cancellationToken)
    {
        CatalogEntry entry = ComponentCatalog.Of(ComponentIds.Standalone);
        if (await NetFallback.OrNullAsync(releases.GetLatestTagAsync(entry.Source, cancellationToken)) is not { } tag)
        {
            return null;
        }

        string name = $"DLSS5-ReShade-AIO-{tag}-64-bit.zip";
        Uri url = entry.Repository.ReleaseDownload(tag, name);
        return await ExistsAsync(url, cancellationToken) ? Guessed(entry, tag, name, url) : null;
    }

    private static ComponentRelease Guessed(CatalogEntry entry, string tag, string name, Uri url) =>
        new(
            ComponentIds.Standalone,
            new(tag),
            [new(name, url, entry.Unchecked(ReleaseAssets.NoReleaseData))],
            _standaloneLayout,
            new(ReleaseOrigin.Pages, TimeSpan.Zero));

    // A HEAD is never followed: GitHub answers an asset's address with a redirect to its storage, so
    // a 3xx says the asset exists as much as a 2xx does. A network failure says it does not.
    private async Task<bool> ExistsAsync(Uri url, CancellationToken cancellationToken) =>
        await NetFallback.OrNullAsync(client.HeadAsync(url, ComponentHosts.Allowlist, cancellationToken))
            is { StatusCode: >= System.Net.HttpStatusCode.OK and < System.Net.HttpStatusCode.BadRequest };
}
