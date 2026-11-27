using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components.Sources;

/// <summary>
/// The feeder's release list pages, walked by tag alone when the 15 newest releases are all test
/// builds: github.com's list pages go back about twice as far as the API's 15.
/// </summary>
internal sealed partial class FeederPages(GitHubReleases releases, ILogger logger)
{
    private const int WalkedPages = 2;

    /// <summary>
    /// The newest release whose tag carries no test word; <see langword="null"/> when the pages list
    /// none, or failed (logged).
    /// </summary>
    public async Task<ReleaseAnswer?> NewestReleaseAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string>? tags = await NetFallback.OrNullAsync(releases.GetPageTagsAsync(ComponentRepositories.Feeder, WalkedPages, cancellationToken), failure =>
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                LogWalkFailed(logger, failure.Kind);
            }
        });
        string? tag = tags?.FirstOrDefault(tag => !new ComponentVersion(tag).IsTestBuild);
        return tag is null ? null : await releases.GetByTagAsync(ComponentRepositories.Feeder, tag, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Only test builds among the newest DLSS5-Feeder releases, and github.com's release pages failed ({Kind})")]
    private static partial void LogWalkFailed(ILogger logger, NetErrorKind kind);
}
