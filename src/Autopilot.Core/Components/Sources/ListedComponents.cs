using System.Text.RegularExpressions;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Graphics;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components.Sources;

/// <summary>
/// The components found in a release list or under a pinned tag: GITC-Uplift (the newest of the 10
/// newest releases that carries its zip), RTX40MFG-Unlock (its pinned tag, then the newest release of
/// the same shape) and Ultimate ASI Loader (its pinned tag only).
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

    private const string MfgPrefix = "universal-rtx-40-mfg-unlock";

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
        ReleaseListAnswer list = await releases.GetListAsync(ComponentCatalog.Of(ComponentIds.Uplift).Source, UpliftReleases, cancellationToken);
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
        string repository = ComponentCatalog.Of(ComponentIds.Mfg).Source;
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
        string repository = ComponentCatalog.Of(ComponentIds.AsiLoader).Source;
        ReleaseAnswer pinned = await releases.GetByTagAsync(repository, AsiLoaderPin, cancellationToken)
            ?? throw new ComponentException(ComponentFailure.NoRelease, Texts.Format("ComponentNoTag", repository, AsiLoaderPin));
        return ReleaseAssets.ByName(ComponentIds.AsiLoader, pinned, [AsiLoaderAsset], [new(KnownFiles.Dinput8)]);
    }

    private static GitHubAsset? MfgZip(GitHubRelease release) =>
        release.Assets.FirstOrDefault(asset =>
            asset.Name.StartsWith(MfgPrefix, StringComparison.OrdinalIgnoreCase) && asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"^GITC-Uplift-v?\d[\w.\-]*\.zip$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UpliftZip();
}
