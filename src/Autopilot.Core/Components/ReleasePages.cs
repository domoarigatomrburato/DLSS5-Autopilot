using Autopilot.Core.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components;

/// <summary>
/// Release data read from github.com's own pages, for when the API failed and nothing was kept: the
/// list from <c>/releases?page=n</c>, one release's assets from <c>/releases/expanded_assets/&lt;tag&gt;</c>,
/// and "latest" from the <c>/releases/latest</c> redirect. Nothing here spends API allowance, and no
/// flag or date is known. <see langword="null"/> means the pages could not answer.
/// </summary>
internal sealed partial class ReleasePages(NetClient client, ILogger logger)
{
    public const int MaxPages = 4;

    // Each release costs a page of its own, so the list stops here.
    public const int MaxReleases = 12;

    /// <summary>The release the <c>latest</c> redirect points at: one HEAD, never followed.</summary>
    public async Task<GitHubRelease?> LatestAsync(GitHubRepository repo, CancellationToken cancellationToken)
    {
        HeadAnswer head = await client.HeadAsync(repo.Latest, ComponentHosts.Allowlist, cancellationToken);
        return head.Location is { } location && repo.TagIn(location) is { } tag ? await ReleaseAsync(repo, tag, cancellationToken) : null;
    }

    public async Task<GitHubRelease?> ReleaseAsync(GitHubRepository repo, string tag, CancellationToken cancellationToken)
    {
        Uri fragment = repo.ExpandedAssets(tag);
        TextAnswer answer = await client.GetTextAsync(fragment, ComponentHosts.Allowlist, null, cancellationToken);
        return IsSuccess(answer)
            ? new(tag, Prerelease: null, Draft: null, PublishedAt: null, ReleaseHtml.Assets(answer.Body, fragment, repo))
            : null;
    }

    /// <summary>Up to <paramref name="count"/> releases, at most <see cref="MaxReleases"/>, newest first, each with its assets.</summary>
    public async Task<IReadOnlyList<GitHubRelease>?> ListAsync(GitHubRepository repo, int count, CancellationToken cancellationToken)
    {
        List<string> tags = await TagsAsync(repo, Math.Min(count, MaxReleases), cancellationToken);
        List<GitHubRelease> releases = [];
        foreach (string tag in tags)
        {
            if (await ReleaseAsync(repo, tag, cancellationToken) is not { } release)
            {
                return null;
            }

            releases.Add(release);
        }

        return releases.Count > 0 ? releases : null;
    }

    /// <summary>Tags from the list pages, until <paramref name="count"/> are found, a page adds none, or the pages run out.</summary>
    private async Task<List<string>> TagsAsync(GitHubRepository repo, int count, CancellationToken cancellationToken)
    {
        List<string> tags = [];
        for (int page = 1; page <= MaxPages && tags.Count < count; page++)
        {
            IReadOnlyList<string> found = await TagsOnPageAsync(repo, page, cancellationToken);
            int before = tags.Count;
            tags.AddRange(found.Except(tags, StringComparer.Ordinal).Take(count - tags.Count));
            if (tags.Count == before)
            {
                break;
            }
        }

        return tags;
    }

    /// <summary>
    /// One list page's tags, none when it does not answer. A network failure on the first page
    /// fails the pages; on a later one it ends the list there.
    /// </summary>
    public async Task<IReadOnlyList<string>> TagsOnPageAsync(GitHubRepository repo, int page, CancellationToken cancellationToken)
    {
        Uri url = repo.ReleasesPage(page);
        try
        {
            TextAnswer answer = await client.GetTextAsync(url, ComponentHosts.Allowlist, null, cancellationToken);
            return IsSuccess(answer) ? ReleaseHtml.Tags(answer.Body, url, repo) : [];
        }
        catch (NetException failure) when (page > 1 && !failure.IsRefusal)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                LogPageFailed(logger, url.AbsolutePath, page, failure.Kind);
            }

            return [];
        }
    }

    private static bool IsSuccess(TextAnswer answer) => (int)answer.StatusCode is >= 200 and <= 299;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Release list page {Page} of {Path} failed ({Kind}); the list ends before it")]
    private static partial void LogPageFailed(ILogger logger, string path, int page, NetErrorKind kind);
}
