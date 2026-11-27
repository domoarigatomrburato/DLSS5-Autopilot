using System.Globalization;
using System.Net;
using Autopilot.Core.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components.GitHub;

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

    // Several components pick from the same list (the feeder's choices, wilsjo2's two builds): it is read once per run.
    private readonly ResolutionMemory<(string Repository, int Count), ReleaseListAnswer> _lists = new();

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
    public Task<ReleaseListAnswer> GetListAsync(string repository, int count, CancellationToken cancellationToken) =>
        GetListAsync(repository, count, (pages, repo, token) => pages.ListAsync(repo, count, token), cancellationToken);

    /// <summary>
    /// <see cref="GetListAsync(string, int, CancellationToken)"/>, read once per run: the same answer
    /// for the rest of the run once read, for the callers that pick from one list several times.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="repository"/> is not <c>owner/name</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not 1 to <see cref="MaxListCount"/>.</exception>
    /// <exception cref="NetException">Neither the API, nor a kept answer, nor the pages could answer, or the API does not know the repository.</exception>
    /// <exception cref="ReleaseDataException">The API's answer is not release data.</exception>
    internal Task<ReleaseListAnswer> GetListOnceAsync(string repository, int count, CancellationToken cancellationToken) =>
        _lists.GetAsync((repository, count), token => GetListAsync(repository, count, token), cancellationToken);

    /// <summary>
    /// Up to <paramref name="count"/> releases, newest first, with the caller's own reading of
    /// github.com's pages when the API fails and nothing was kept.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="repository"/> is not <c>owner/name</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not 1 to <see cref="MaxListCount"/>.</exception>
    /// <exception cref="NetException">Neither the API, nor a kept answer, nor the pages could answer, or the API does not know the repository.</exception>
    /// <exception cref="ReleaseDataException">The API's answer is not release data.</exception>
    internal async Task<ReleaseListAnswer> GetListAsync(
        string repository,
        int count,
        Func<ReleasePages, GitHubRepository, CancellationToken, Task<IReadOnlyList<GitHubRelease>?>> pagesFallback,
        CancellationToken cancellationToken)
    {
        var repo = GitHubRepository.Parse(repository);
        Uri api = ListApi(repo, count);
        (IReadOnlyList<GitHubRelease>? releases, ReleaseSource source) = await _resolver.ResolveAsync(
            api,
            ReleaseJson.List,
            token => pagesFallback(_pages, repo, token),
            cancellationToken);
        return releases is not null ? new(releases, source) : throw ReleaseResolver.StatusFailure(api, HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The release tags on the first <paramref name="pages"/> of github.com's release list pages,
    /// newest first: no API allowance spent, no asset page read, no flag known.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="repository"/> is not <c>owner/name</c>.</exception>
    /// <exception cref="NetException">The first page could not be read.</exception>
    internal Task<IReadOnlyList<string>> GetPageTagsAsync(string repository, int pages, CancellationToken cancellationToken) =>
        _pages.TagsOnPagesAsync(GitHubRepository.Parse(repository), pages, cancellationToken);

    /// <summary>The tag GitHub's <c>releases/latest</c> redirect points at; <see langword="null"/> when it names no release.</summary>
    /// <exception cref="ArgumentException"><paramref name="repository"/> is not <c>owner/name</c>.</exception>
    /// <exception cref="NetException">The redirect could not be asked.</exception>
    internal Task<string?> GetLatestTagAsync(string repository, CancellationToken cancellationToken) =>
        _pages.LatestTagAsync(GitHubRepository.Parse(repository), cancellationToken);

    private static Uri ListApi(GitHubRepository repo, int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxListCount);
        return repo.Api("?per_page=" + count.ToString(CultureInfo.InvariantCulture));
    }

    private async Task<ReleaseAnswer?> GetOneAsync(Uri api, Func<CancellationToken, Task<GitHubRelease?>> fromPages, CancellationToken cancellationToken)
    {
        (GitHubRelease? release, ReleaseSource source) = await _resolver.ResolveAsync(api, ReleaseJson.One, fromPages, cancellationToken);
        return release is not null ? new(release, source) : null;
    }
}
