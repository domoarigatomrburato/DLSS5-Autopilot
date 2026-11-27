using Autopilot.Core.Components.GitHub;

namespace Autopilot.Core.Components.Mirror;

/// <summary>The mirror's releases as build entries: a release of a known family carrying a <c>.zip</c> is one entry.</summary>
internal static class MirrorReleases
{
    /// <summary>
    /// The releases as families, each newest first by the integers in its labels; entries whose
    /// versions cannot say which is newer (<c>310.8.SF</c> and <c>310.8.Lecram</c>) by tag, ordinal,
    /// descending like the rest of the list, so the order is strict and the same on every run,
    /// whatever the list's order (and <c>310.9.16-nvidia</c> stays ahead of <c>310.9.16</c>).
    /// </summary>
    public static Dictionary<BuildFamily, IReadOnlyList<BuildEntry>> Group(IEnumerable<GitHubRelease> releases) =>
        releases
            .Select(Entry)
            .OfType<BuildEntry>()
            .GroupBy(entry => entry.Family)
            .ToDictionary(
                family => family.Key,
                family => (IReadOnlyList<BuildEntry>)[
                    .. family
                        .OrderByDescending(entry => new ComponentVersion(entry.Label), ComponentVersion.Order)
                        .ThenByDescending(entry => entry.Tag, StringComparer.Ordinal)]);

    /// <summary>
    /// The release's entry, its zip checked by the digest GitHub shows for it (from the API or the
    /// pages) as the family's mirror row says; <see langword="null"/> for a tag of no family or a release without a zip.
    /// </summary>
    public static BuildEntry? Entry(GitHubRelease release) =>
        MirrorFamilies.Classify(release.Tag) is var (family, label) && Zip(release) is { } zip
            ? new(family, BuildPublisher.Mirror, release.Tag, label, zip.Url, zip.Name, MirrorFamilies.EntryOf(family).DigestOr(zip))
            : null;

    private static GitHubAsset? Zip(GitHubRelease release) =>
        release.Assets.FirstOrDefault(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
}
