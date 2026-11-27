using System.Text.RegularExpressions;
using Autopilot.Core.Archives;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;
using Autopilot.Core.Resources;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components.Sources;

/// <summary>
/// The OptiScaler builds: each the newest release of its page that carries an OptiScaler package,
/// resolved once per build per run; then the package fetched, unpacked in the cache, and refused
/// when it holds no root-level <c>OptiScaler.dll</c>, before anything is written to a game.
/// </summary>
public sealed partial class OptiScalerSource
{
    private readonly ResolutionMemory<string, OptiScalerRelease> _memory;
    private readonly OptiScalerReleases _releases;

    private readonly DownloadCache _cache;

    /// <summary>Built by a <see cref="ComponentRun"/>; <paramref name="run"/> is its token, which the shared resolutions run under.</summary>
    internal OptiScalerSource(GitHubReleases releases, DownloadCache cache, ILogger<OptiScalerSource> logger, CancellationToken run)
    {
        _memory = new(run);
        _releases = new(releases, logger);
        _cache = cache;
    }

    /// <summary>The release and package <paramref name="build"/> installs; the same answer for the rest of the run once found.</summary>
    /// <exception cref="ComponentException">No recent release carries the package, or a build that publishes sums carries none.</exception>
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
    /// <exception cref="ComponentException">The package holds no root-level <c>OptiScaler.dll</c>: it is not an OptiScaler build.</exception>
    /// <exception cref="NetException">The download failed, its content is wrong, or its verification failed (sums that cannot be read included).</exception>
    /// <exception cref="ArchiveException">The archive was refused.</exception>
    /// <exception cref="Storage.DiskFullException">The drive filled up while the package was kept.</exception>
    public async Task<OptiScalerPackage> FetchAsync(OptiScalerRelease release, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        CachedFile archive = await _cache.DownloadAsync(
            release.Archive.Url,
            ComponentHosts.Allowlist,
            release.CacheName,
            release.Verification,
            null,
            progress,
            cancellationToken);
        string unpacked = await _cache.UnpackAsync(archive, null, null, cancellationToken);
        return new(release, OptiScalerFolder.PackageRoot(unpacked, release.Tag), archive);
    }

    private async Task<OptiScalerRelease> ResolveOnceAsync(OptiScalerBuild build, CancellationToken cancellationToken)
    {
        (GitHubRelease release, GitHubAsset archive, ReleaseSource source) = await _releases.FirstWithPackageAsync(build, cancellationToken)
            ?? throw NoPackage(build);
        return new(build, release.Tag, archive, build.VerificationOf(release, archive), source);
    }

    private static ComponentException NoPackage(OptiScalerBuild build) =>
        build.VariantWord is { } variant
            ? new(ComponentFailure.NoPackage, Texts.Format("OptiScalerNoVariant", build.Repository, variant))
            : new(ComponentFailure.NoPackage, Texts.Format("OptiScalerNoPackage", build.Repository));
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
