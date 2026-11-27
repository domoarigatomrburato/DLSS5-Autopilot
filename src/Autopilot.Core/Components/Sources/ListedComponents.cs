using System.Text.RegularExpressions;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Graphics;
using Autopilot.Core.Net;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components.Sources;

/// <summary>
/// The components found in a release list or under a pinned tag: GITC-Uplift (the newest of the 10
/// newest releases that carries its zip), RTX40MFG-Unlock (its pinned tag, then the newest release of
/// the same shape), Ultimate ASI Loader and the Remix runtime (each its pinned tag only).
/// </summary>
internal sealed partial class ListedComponents(GitHubReleases releases)
{
    /// <summary>How many of GITC-Uplift's newest releases are searched for its zip.</summary>
    public const int UpliftReleases = 10;

    /// <summary>How many of the MFG unlock's newest releases are searched when its pinned tag carries no zip.</summary>
    public const int MfgReleases = 20;

    public const string AsiLoaderAsset = "Ultimate-ASI-Loader_x64.zip";

    /// <summary>The RTX40MFG-Unlock release asked for by its tag first: the one this route was last run against.</summary>
    internal const string MfgPin = "v1.2.1";

    /// <summary>The Ultimate ASI Loader release used; v10 exists and nobody has run it with this route yet.</summary>
    internal const string AsiLoaderPin = "v9.7.4";

    /// <summary>
    /// The Remix runtime release used: the fork's one neural-rendering build. Its other releases
    /// (another project's pre-releases and zips) would be <c>/latest</c> if one were published.
    /// </summary>
    internal const string RemixRuntimePin = "dlssnr-v1";

    private const string MfgPrefix = "universal-rtx-40-mfg-unlock";

    private static readonly string[] _remixRuntimeFiles = [KnownFiles.D3d9, KnownFiles.RemixNgx];

    private static readonly ExpectedFile[] _upliftLayout =
        [.. new[] { "gitc-uplift.addon64", "gitc-uplift.addon32", "gitc-uplift-helper64.exe", "Uplift.fx", "UpliftMask.fx" }.Select(name => new ExpectedFile(name))];

    private static readonly ExpectedFile[] _mfgLayout = [.. new[] { "RTX40MFGCore.dll", "RTX40MFG.asi", "RTX40MFG-UI.addon64" }.Select(name => new ExpectedFile(name))];

    /// <summary>
    /// The newest release, drafts and pre-releases dropped, that carries <c>GITC-Uplift-&lt;version&gt;.zip</c>,
    /// checked against its <c>SHA256SUMS.txt</c>. There is no fallback to a zip already in the cache:
    /// the reference installed that one unchecked, and known sums that cannot be read stop the install.
    /// </summary>
    public async Task<ComponentRelease> UpliftAsync(CancellationToken cancellationToken)
    {
        ReleaseListAnswer list = await releases.GetListAsync(ComponentCatalog.Of(ComponentIds.Uplift).Repository, UpliftReleases, cancellationToken);
        return ReleaseAssets.FirstPublishedCarrying(
            ComponentIds.Uplift,
            list,
            release => release.Assets.FirstOrDefault(asset => UpliftZip().IsMatch(asset.Name)),
            "GITC-Uplift-<version>.zip",
            _upliftLayout);
    }

    /// <summary>The pinned tag first; then the newest release carrying the universal zip. A release of another shape is passed over.</summary>
    public async Task<ComponentRelease> MfgAsync(CancellationToken cancellationToken)
    {
        GitHubRepository repository = ComponentCatalog.Of(ComponentIds.Mfg).Repository;
        if (await releases.GetByTagAsync(repository, MfgPin, cancellationToken) is { } pinned
            && MfgZip(pinned.Release) is { } pinnedZip)
        {
            return ReleaseAssets.Of(ComponentIds.Mfg, pinned, [pinnedZip], _mfgLayout);
        }

        ReleaseListAnswer list = await releases.GetListAsync(repository, MfgReleases, cancellationToken);
        return ReleaseAssets.FirstPublishedCarrying(ComponentIds.Mfg, list, MfgZip, "Universal-RTX-40-MFG-Unlock-<version>.zip", _mfgLayout);
    }

    /// <summary>The pinned tag's 64-bit zip, holding the loader as <c>dinput8.dll</c>.</summary>
    public async Task<ComponentRelease> AsiLoaderAsync(CancellationToken cancellationToken)
    {
        ReleaseAnswer pinned = await PinnedAsync(ComponentCatalog.Of(ComponentIds.AsiLoader).Repository, AsiLoaderPin, cancellationToken);
        return ReleaseAssets.ByName(ComponentIds.AsiLoader, pinned, [AsiLoaderAsset], [new(KnownFiles.Dinput8)]);
    }

    /// <summary>
    /// The pinned tag's two runtime files, checked against its sums file when it carries one; with no
    /// release data, each by the tag's own download address, checked by its magic bytes only.
    /// </summary>
    /// <exception cref="ComponentException">The tag is not on GitHub, or its release lacks one of the files: it changed its layout.</exception>
    public async Task<ComponentRelease> RemixRuntimeAsync(CancellationToken cancellationToken)
    {
        GitHubRepository repository = ComponentCatalog.Of(ComponentIds.RemixRuntime).Repository;
        ReleaseAnswer pinned;
        try
        {
            pinned = await PinnedAsync(repository, RemixRuntimePin, cancellationToken);
        }
        catch (NetException failure) when (!failure.IsRefusal)
        {
            // Neither the API, a kept answer nor the pages answered: the tag's own download addresses, with no digest known.
            return ReleaseAssets.ByDownload(
                ComponentIds.RemixRuntime,
                new(RemixRuntimePin),
                name => repository.ReleaseDownload(RemixRuntimePin, name),
                _remixRuntimeFiles);
        }

        return ReleaseAssets.ByName(ComponentIds.RemixRuntime, pinned, _remixRuntimeFiles, [.. _remixRuntimeFiles.Select(name => new ExpectedFile(name))]);
    }

    /// <summary>The release tagged <paramref name="tag"/>, the one this tool places.</summary>
    /// <exception cref="ComponentException">GitHub has no release under the tag.</exception>
    private async Task<ReleaseAnswer> PinnedAsync(GitHubRepository repository, string tag, CancellationToken cancellationToken) =>
        await releases.GetByTagAsync(repository, tag, cancellationToken)
            ?? throw new ComponentException(ComponentFailure.NoRelease, Texts.Format("ComponentNoTag", repository, tag));

    private static GitHubAsset? MfgZip(GitHubRelease release) =>
        release.Assets.FirstOrDefault(asset =>
            asset.Name.StartsWith(MfgPrefix, StringComparison.OrdinalIgnoreCase) && asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"^GITC-Uplift-v?\d[\w.\-]*\.zip$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UpliftZip();
}
