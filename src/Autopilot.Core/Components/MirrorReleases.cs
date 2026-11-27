using Autopilot.Core.Net;

namespace Autopilot.Core.Components;

/// <summary>The mirror's releases as build entries: a release of a known family carrying a <c>.zip</c> is one entry.</summary>
internal static class MirrorReleases
{
    private static readonly IComparer<string> _newestFirst = Comparer<string>.Create(
        (left, right) => new ComponentVersion(right).CompareTo(new ComponentVersion(left)) switch
        {
            VersionOrder.Older => -1,
            VersionOrder.Newer => 1,
            _ => 0,
        });

    /// <summary>The releases as families, each newest first by the integers in its labels; equal ones keep the list's order.</summary>
    public static Dictionary<BuildFamily, IReadOnlyList<BuildEntry>> Group(IEnumerable<GitHubRelease> releases) =>
        releases
            .Select(Entry)
            .OfType<BuildEntry>()
            .GroupBy(entry => entry.Family)
            .ToDictionary(family => family.Key, family => (IReadOnlyList<BuildEntry>)[.. family.OrderBy(entry => entry.Label, _newestFirst)]);

    /// <summary>The release's entry; <see langword="null"/> for a tag of no family or a release without a zip.</summary>
    public static BuildEntry? Entry(GitHubRelease release) =>
        MirrorFamilies.Classify(release.Tag) is var (family, label) && Zip(release) is { } zip
            ? new(family, BuildPublisher.Mirror, release.Tag, label, zip.Url, zip.Name, Verification(zip))
            : null;

    private static GitHubAsset? Zip(GitHubRelease release) =>
        release.Assets.FirstOrDefault(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

    // The digest GitHub shows for the asset, from the API or the pages.
    private static Verification Verification(GitHubAsset zip) =>
        zip.Digest ?? (Verification)new NoVerification("the mirror's release names no SHA-256 for this file");
}
