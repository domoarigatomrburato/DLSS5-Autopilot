using Autopilot.Core.Components.GitHub;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components.Sources;

/// <summary>
/// Which release of an OptiScaler build's page carries its package: the default build's
/// <c>/latest</c> first, then the newest of the page's list, which <see cref="GitHubReleases"/> reads
/// once per run: wilsjo2's two builds share one.
/// </summary>
internal sealed class OptiScalerReleases(GitHubReleases releases, ILogger logger)
{
    /// <summary>The release carrying <paramref name="build"/>'s package, the package, and where the data came from; <see langword="null"/> when none does.</summary>
    public async Task<(GitHubRelease Release, GitHubAsset Archive, ReleaseSource Source)?> FirstWithPackageAsync(OptiScalerBuild build, CancellationToken cancellationToken)
    {
        string? latestTag = null;
        if (build.Key == OptiScalerBuild.Default.Key
            && await releases.GetLatestAsync(build.Repository, cancellationToken) is { } latest)
        {
            if (Package(latest.Release, build) is { } package)
            {
                return (latest.Release, package, latest.Source);
            }

            // The default build's "latest" holds no package: the list, newest first, without it.
            latestTag = latest.Release.Tag;
        }

        ReleaseListAnswer list = await releases.GetListOnceAsync(build.Repository, OptiScalerSource.ListedReleases, cancellationToken);
        return ReleaseAssets.FirstCarrying(Candidates(list.Releases).Where(release => release.Tag != latestTag), release => Package(release, build)) is var (found, archive)
            ? (found, archive, list.Source)
            : null;
    }

    private GitHubAsset? Package(GitHubRelease release, OptiScalerBuild build) =>
        IsCandidate(release) ? OptiScalerPackageChoice.Choose(release, build, logger) : null;

    // Newest published first; drafts and "nightly" (a fixed tag, which would be cached once and
    // never refreshed) dropped. The pre-release flag is ignored: the forks flag the release wanted.
    // The pages give no dates, and keep their own order.
    private static IEnumerable<GitHubRelease> Candidates(IEnumerable<GitHubRelease> releases) =>
        releases.Where(IsCandidate).OrderByDescending(release => release.PublishedAt ?? DateTimeOffset.MinValue);

    private static bool IsCandidate(GitHubRelease release) =>
        release.Draft != true && !string.Equals(release.Tag, "nightly", StringComparison.OrdinalIgnoreCase);
}
