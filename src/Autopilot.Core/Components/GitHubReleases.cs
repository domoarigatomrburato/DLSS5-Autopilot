using System.Globalization;
using System.Net;
using Autopilot.Core.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components;

/// <summary>
/// A GitHub repository's releases: from the API, kept for 6 hours; from a kept answer of any age when
/// the API fails; and from github.com's own release pages when nothing was kept. A refusal (a host
/// off the list, a downgrade, an answer too large) is never covered by another source.
/// </summary>
public sealed class GitHubReleases(NetClient client, TimeProvider time, ILogger<GitHubReleases> logger)
{
    /// <summary>The most releases the API lists in one answer.</summary>
    public const int MaxListCount = 100;

    private readonly ReleasePages _pages = new(client, logger);
    private readonly ReleaseResolver _resolver = new(client, time, logger);

    /// <summary>The newest release that is neither a draft nor a pre-release, as GitHub names it; <see langword="null"/> when the repository has none.</summary>
    /// <exception cref="ArgumentException"><paramref name="repository"/> is not <c>owner/name</c>.</exception>
    /// <exception cref="NetException">Neither the API, nor a kept answer, nor the pages could answer.</exception>
    /// <exception cref="ReleaseDataException">The API's answer is not release data.</exception>
    public async Task<ReleaseAnswer?> GetLatestAsync(string repository, CancellationToken cancellationToken)
    {
        var repo = GitHubRepository.Parse(repository);
        return await GetOneAsync(repo.Api("/latest"), token => _pages.LatestAsync(repo, token), cancellationToken);
    }

    /// <summary>The release tagged <paramref name="tag"/>; <see langword="null"/> when the API says there is none.</summary>
    /// <exception cref="ArgumentException"><paramref name="repository"/> is not <c>owner/name</c>, or the tag is empty.</exception>
    /// <exception cref="NetException">Neither the API, nor a kept answer, nor the pages could answer.</exception>
    /// <exception cref="ReleaseDataException">The API's answer is not release data.</exception>
    public async Task<ReleaseAnswer?> GetByTagAsync(string repository, string tag, CancellationToken cancellationToken)
    {
        var repo = GitHubRepository.Parse(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return await GetOneAsync(repo.Api("/tags/" + Uri.EscapeDataString(tag)), token => _pages.ReleaseAsync(repo, tag, token), cancellationToken);
    }

    /// <summary>
    /// Up to <paramref name="count"/> releases, newest first. From the pages, at most 12, with no
    /// flags or dates known.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="repository"/> is not <c>owner/name</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not 1 to <see cref="MaxListCount"/>.</exception>
    /// <exception cref="NetException">Neither the API, nor a kept answer, nor the pages could answer, or the API does not know the repository.</exception>
    /// <exception cref="ReleaseDataException">The API's answer is not release data.</exception>
    public async Task<ReleaseListAnswer> GetListAsync(string repository, int count, CancellationToken cancellationToken)
    {
        var repo = GitHubRepository.Parse(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxListCount);
        Uri api = repo.Api("?per_page=" + count.ToString(CultureInfo.InvariantCulture));
        (IReadOnlyList<GitHubRelease>? releases, ReleaseSource source) = await _resolver.ResolveAsync(
            api,
            ReleaseJson.List,
            token => _pages.ListAsync(repo, count, token),
            cancellationToken);
        return releases is not null ? new(releases, source) : throw ReleaseResolver.StatusFailure(api, HttpStatusCode.NotFound);
    }

    /// <summary>The address of an asset of the newest release by GitHub's own redirect, for when no release data can be read.</summary>
    /// <exception cref="ArgumentException"><paramref name="repository"/> is not <c>owner/name</c>, or the asset name is empty.</exception>
    public static LatestDownload LatestDownloadOf(string repository, string assetName)
    {
        var repo = GitHubRepository.Parse(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);
        return new(repo.LatestDownload(assetName), ComponentVersion.Latest);
    }

    private async Task<ReleaseAnswer?> GetOneAsync(Uri api, Func<CancellationToken, Task<GitHubRelease?>> fromPages, CancellationToken cancellationToken)
    {
        (GitHubRelease? release, ReleaseSource source) = await _resolver.ResolveAsync(api, ReleaseJson.One, fromPages, cancellationToken);
        return release is not null ? new(release, source) : null;
    }
}
