using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;

namespace Autopilot.Core.Components.Mirror;

/// <summary>The mirror's releases as build entries: a release of a known family carrying a <c>.zip</c> is one entry.</summary>
internal static class MirrorReleases
{
    /// <summary>The releases as families, each newest first by the integers in its labels; equal ones keep the list's order.</summary>
    public static Dictionary<BuildFamily, IReadOnlyList<BuildEntry>> Group(IEnumerable<GitHubRelease> releases) =>
        releases
            .Select(Entry)
            .OfType<BuildEntry>()
            .GroupBy(entry => entry.Family)
            .ToDictionary(family => family.Key, family => (IReadOnlyList<BuildEntry>)[.. family.OrderByDescending(entry => new ComponentVersion(entry.Label), ComponentVersion.Order)]);

    /// <summary>The release's entry; <see langword="null"/> for a tag of no family or a release without a zip.</summary>
    public static BuildEntry? Entry(GitHubRelease release) =>
        MirrorFamilies.Classify(release.Tag) is var (family, label) && Zip(release) is { } zip
            ? new(family, BuildPublisher.Mirror, release.Tag, label, zip.Url, zip.Name, CheckedBy(family).DigestOr(zip))
            : null;

    private static GitHubAsset? Zip(GitHubRelease release) =>
        release.Assets.FirstOrDefault(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

    // The catalog row a mirror zip is checked by: the digest GitHub shows for it, from the API or the
    // pages. The RenoDX add-ons go by their own rows; NVIDIA's runtimes on the mirror by the mirror's own NVIDIA row.
    private static CatalogEntry CheckedBy(BuildFamily family) =>
        MirrorFamilies.EntryOf(family) is { Id: ComponentIds.Renodx or ComponentIds.RenodxSf } entry ? entry : ComponentCatalog.Of(ComponentIds.Dlssnr);
}
