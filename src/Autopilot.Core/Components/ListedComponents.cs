using System.Text.RegularExpressions;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components;

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
        ReleaseListAnswer list = await releases.GetListAsync(ComponentRepositories.Uplift, UpliftReleases, cancellationToken);
        foreach (GitHubRelease release in ReleaseAssets.Published(list.Releases))
        {
            if (release.Assets.FirstOrDefault(asset => UpliftZip().IsMatch(asset.Name)) is { } zip)
            {
                return ReleaseAssets.Of(ComponentIds.Uplift, new(release, list.Source), [zip], _upliftLayout);
            }
        }

        throw ReleaseAssets.NoneCarries(ComponentIds.Uplift, "GITC-Uplift-<version>.zip", list.Releases);
    }

    /// <summary>The pinned tag first; then the newest release carrying the universal zip. A release of another shape is passed over.</summary>
    public async Task<ComponentRelease> MfgAsync(CancellationToken cancellationToken)
    {
        if (await releases.GetByTagAsync(ComponentRepositories.Mfg, ComponentSources.MfgPin, cancellationToken) is { } pinned
            && MfgZip(pinned.Release) is { } pinnedZip)
        {
            return ReleaseAssets.Of(ComponentIds.Mfg, pinned, [pinnedZip], _mfgLayout);
        }

        ReleaseListAnswer list = await releases.GetListAsync(ComponentRepositories.Mfg, MfgReleases, cancellationToken);
        foreach (GitHubRelease release in ReleaseAssets.Published(list.Releases))
        {
            if (MfgZip(release) is { } zip)
            {
                return ReleaseAssets.Of(ComponentIds.Mfg, new(release, list.Source), [zip], _mfgLayout);
            }
        }

        throw ReleaseAssets.NoneCarries(ComponentIds.Mfg, "Universal-RTX-40-MFG-Unlock-<version>.zip", list.Releases);
    }

    /// <summary>The pinned tag's 64-bit zip, holding the loader as <c>dinput8.dll</c>.</summary>
    public async Task<ComponentRelease> AsiLoaderAsync(CancellationToken cancellationToken)
    {
        ReleaseAnswer pinned = await releases.GetByTagAsync(ComponentRepositories.AsiLoader, ComponentSources.AsiLoaderPin, cancellationToken)
            ?? throw new ComponentException(ComponentFailure.NoRelease, Texts.Format("ComponentNoTag", ComponentRepositories.AsiLoader, ComponentSources.AsiLoaderPin));
        return ReleaseAssets.Of(ComponentIds.AsiLoader, pinned, ReleaseAssets.Named(ComponentIds.AsiLoader, pinned.Release, [AsiLoaderAsset]), [new("dinput8.dll")]);
    }

    private static GitHubAsset? MfgZip(GitHubRelease release) =>
        release.Assets.FirstOrDefault(asset =>
            asset.Name.StartsWith(MfgPrefix, StringComparison.OrdinalIgnoreCase) && asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"^GITC-Uplift-v?\d[\w.\-]*\.zip$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UpliftZip();
}
