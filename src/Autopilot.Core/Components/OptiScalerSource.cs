using System.Text.RegularExpressions;
using Autopilot.Core.Archives;
using Autopilot.Core.Net;
using Autopilot.Core.Resources;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components;

/// <summary>
/// The OptiScaler builds: each the newest release of its page that carries an OptiScaler package,
/// resolved once per build per run; then the package fetched, unpacked in the cache, and refused
/// when it holds no root-level <c>OptiScaler.dll</c>, before anything is written to a game.
/// </summary>
public sealed partial class OptiScalerSource(GitHubReleases releases, DownloadCache cache, ILogger<OptiScalerSource> logger)
{
    /// <summary>The newest releases a page is searched in for a package.</summary>
    public const int ListedReleases = 10;

    private readonly ResolutionMemory<string, OptiScalerRelease> _memory = new();

    /// <summary>The release and package <paramref name="build"/> installs; the same answer for the rest of the run once found.</summary>
    /// <exception cref="OptiScalerException">No recent release carries the package, or a build that publishes sums carries none.</exception>
    /// <exception cref="NetException">Neither GitHub's API, nor a kept answer, nor the pages could answer.</exception>
    /// <exception cref="ReleaseDataException">The API's answer is not release data.</exception>
    public Task<OptiScalerRelease> ResolveAsync(OptiScalerBuild build, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        return _memory.GetAsync(build.Key, token => ResolveOnceAsync(build, token), cancellationToken);
    }

    /// <summary>
    /// The package in the download cache, checked and unpacked: the folder that holds
    /// <c>OptiScaler.dll</c> at its root (an archive wrapped in one folder is unwrapped).
    /// </summary>
    /// <exception cref="OptiScalerException">The package holds no root-level <c>OptiScaler.dll</c>: it is not an OptiScaler build.</exception>
    /// <exception cref="NetException">The download failed, its content is wrong, or its verification failed (sums that cannot be read included).</exception>
    /// <exception cref="ArchiveException">The archive was refused.</exception>
    /// <exception cref="Storage.DiskFullException">The drive filled up while the package was kept.</exception>
    public async Task<OptiScalerPackage> FetchAsync(OptiScalerRelease release, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        CachedFile archive = await cache.DownloadAsync(
            release.Archive.Url,
            ComponentHosts.Allowlist,
            release.CacheName,
            release.Verification,
            null,
            progress,
            cancellationToken);
        string unpacked = await cache.UnpackAsync(archive, null, null, cancellationToken);
        return new(release, OptiScalerFolder.PackageRoot(unpacked, release.Tag), archive);
    }

    private async Task<OptiScalerRelease> ResolveOnceAsync(OptiScalerBuild build, CancellationToken cancellationToken)
    {
        (GitHubRelease release, GitHubAsset archive, ReleaseSource source) = await FirstWithPackageAsync(build, cancellationToken)
            ?? throw OptiScalerException.NoPackage(build);
        return new(build, release.Tag, archive, OptiScalerVerification.Of(build, release, archive), source);
    }

    private async Task<(GitHubRelease, GitHubAsset, ReleaseSource)?> FirstWithPackageAsync(OptiScalerBuild build, CancellationToken cancellationToken)
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

        ReleaseListAnswer list = await releases.GetListAsync(build.Repository, ListedReleases, cancellationToken);
        foreach (GitHubRelease release in Candidates(list.Releases).Where(release => release.Tag != latestTag))
        {
            if (Package(release, build) is { } package)
            {
                return (release, package, list.Source);
            }
        }

        return null;
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

/// <summary>An OptiScaler build's release and the package chosen from it.</summary>
/// <param name="Build">The build it was resolved for.</param>
/// <param name="Tag">The release's tag, which is the version recorded.</param>
/// <param name="Archive">The package archive.</param>
/// <param name="Verification">How the archive proves itself: the release's sums file or the API's digest when there is one.</param>
/// <param name="Source">Where the release data came from.</param>
public sealed partial record OptiScalerRelease(OptiScalerBuild Build, string Tag, GitHubAsset Archive, Verification Verification, ReleaseSource Source)
{
    // A name part longer than this is cut: the cache name stays well within Windows' limits.
    private const int MaxPart = 80;

    public ComponentVersion Version => new(Tag);

    /// <summary>
    /// The cache name: <c>OptiScaler-DLSSNR-&lt;tag&gt;-&lt;asset stem&gt;&lt;.zip|.7z&gt;</c>, so the two
    /// archives of one release never share an entry.
    /// </summary>
    public string CacheName
    {
        get
        {
            string extension = Archive.Name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ? ".7z" : ".zip";
            return $"OptiScaler-DLSSNR-{Safe(Tag)}-{Safe(Archive.Name[..^extension.Length])}{extension}";
        }
    }

    // Anything but letters, digits, '.', '_' and '-' becomes '_', so a tag or asset name is always a file name.
    private static string Safe(string part)
    {
        string safe = Unsafe().Replace(part, "_");
        return safe.Length > MaxPart ? safe[..MaxPart] : safe;
    }

    [GeneratedRegex("[^A-Za-z0-9._-]+", RegexOptions.CultureInvariant)]
    private static partial Regex Unsafe();
}

/// <summary>An OptiScaler package unpacked in the cache and checked to be one.</summary>
/// <param name="Release">What was resolved.</param>
/// <param name="Folder">The folder holding <c>OptiScaler.dll</c> at its root.</param>
/// <param name="Archive">The verified archive in the download cache.</param>
public sealed record OptiScalerPackage(OptiScalerRelease Release, string Folder, CachedFile Archive);

/// <summary>Why an OptiScaler build could not be resolved or installed.</summary>
public enum OptiScalerFailure
{
    /// <summary>No recent release on the build's page carries its package.</summary>
    NoPackage,

    /// <summary>A build that publishes sums in every release has none in this one.</summary>
    SumsMissing,

    /// <summary>The package holds no root-level <c>OptiScaler.dll</c>: it is some other program.</summary>
    NotOptiScaler,
}

/// <summary>An OptiScaler build refused before anything was written, with the words the install shows.</summary>
public sealed class OptiScalerException(OptiScalerFailure reason, string message) : Exception(message)
{
    public OptiScalerFailure Reason { get; } = reason;

    internal static OptiScalerException NoPackage(OptiScalerBuild build) =>
        build.VariantWord is { } variant
            ? new(OptiScalerFailure.NoPackage, Texts.Format("OptiScalerNoVariant", build.Repository, variant))
            : new(OptiScalerFailure.NoPackage, Texts.Format("OptiScalerNoPackage", build.Repository));
}
