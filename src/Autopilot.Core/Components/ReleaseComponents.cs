using Autopilot.Core.Graphics;

namespace Autopilot.Core.Components;

/// <summary>
/// The components taken from a repository's newest release: dlss5-bridge, neural-upstream,
/// the Remix runtime, DXVK, REFramework. Those taken by asset name have GitHub's
/// <c>latest/download</c> redirect as their last resort, when neither the API, a kept answer nor
/// the pages could answer.
/// </summary>
internal sealed class ReleaseComponents(GitHubReleases releases)
{
    /// <summary>The upstream add-on's name, which is load-bearing: the NGX hook only serves a module whose path holds <c>nvngx.dll</c>.</summary>
    public const string UpstreamAsset = "nvngx.dll.addon64";

    public static readonly string[] RemixRuntimeFiles = [KnownFiles.D3d9, KnownFiles.RemixNgx];

    private static readonly ExpectedFile[] _dxvkLayout =
        [.. new[] { "x64", "x32" }.SelectMany(folder => KnownFiles.DxvkProxies.Select(name => new ExpectedFile($"{folder}/{name}")))];

    /// <summary>The first <c>.addon64</c> of the newest release.</summary>
    public async Task<ComponentRelease> BridgeAsync(CancellationToken cancellationToken)
    {
        ReleaseAnswer latest = await ReleaseAssets.LatestAsync(releases, ComponentIds.Bridge, cancellationToken);
        GitHubAsset addon = ReleaseAssets.First(
            ComponentIds.Bridge,
            latest.Release,
            asset => asset.Name.EndsWith(".addon64", StringComparison.OrdinalIgnoreCase),
            ".addon64");
        return ReleaseAssets.Of(ComponentIds.Bridge, latest, [addon], [new(addon.Name)]);
    }

    public Task<ComponentRelease> UpstreamAsync(CancellationToken cancellationToken) =>
        ReleaseAssets.ByNameAsync(releases, ComponentIds.Upstream, [UpstreamAsset], cancellationToken);

    /// <summary>Both runtime files, checked against the release's sums file when it carries one.</summary>
    public Task<ComponentRelease> RemixRuntimeAsync(CancellationToken cancellationToken) =>
        ReleaseAssets.ByNameAsync(releases, ComponentIds.RemixRuntime, RemixRuntimeFiles, cancellationToken);

    /// <summary>The first <c>dxvk-*.tar.gz</c> that is not the <c>native</c> (Linux) build.</summary>
    public async Task<ComponentRelease> DxvkAsync(CancellationToken cancellationToken)
    {
        ReleaseAnswer latest = await ReleaseAssets.LatestAsync(releases, ComponentIds.Dxvk, cancellationToken);
        GitHubAsset archive = ReleaseAssets.First(
            ComponentIds.Dxvk,
            latest.Release,
            asset => asset.Name.StartsWith("dxvk-", StringComparison.OrdinalIgnoreCase)
                && asset.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                && !asset.Name.Contains("native", StringComparison.OrdinalIgnoreCase),
            "dxvk-<version>.tar.gz");
        return ReleaseAssets.Of(ComponentIds.Dxvk, latest, [archive], _dxvkLayout);
    }

    /// <summary>The nightly repository's <c>REFramework.zip</c>, holding its one <c>dinput8.dll</c>.</summary>
    public async Task<ComponentRelease> ReframeworkAsync(CancellationToken cancellationToken)
    {
        ReleaseAnswer latest = await ReleaseAssets.LatestAsync(releases, ComponentIds.Reframework, cancellationToken);
        return ReleaseAssets.ByName(ComponentIds.Reframework, latest, ["REFramework.zip"], [new(KnownFiles.Dinput8)]);
    }
}
