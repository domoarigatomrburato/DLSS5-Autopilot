using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components.Mirror;

/// <summary>
/// The pinned builds the API's release list no longer reaches: the list stops at 100 releases and the
/// pins are old builds, so each one missing from it is asked for by its tag, like any API answer.
/// </summary>
internal sealed partial class MirrorPins(GitHubReleases releases, ILogger logger)
{
    /// <summary>
    /// The list with every pinned build it lacks and the API has. <c>Complete</c> is false when a pin
    /// could not be asked (the network failed), so the list is not remembered and the next install asks again;
    /// a tag the API does not know stays missing, and a pick that needs it refuses.
    /// </summary>
    public async Task<(IReadOnlyList<GitHubRelease> Releases, bool Complete)> AddMissingAsync(IReadOnlyList<GitHubRelease> list, CancellationToken cancellationToken)
    {
        List<GitHubRelease> all = [.. list];
        bool complete = true;
        foreach (string tag in RenodxPins.Tags.Where(tag => !list.Any(release => release.Tag == tag)))
        {
            ReleaseAnswer? answer = await NetFallback.OrNullAsync(releases.GetByTagAsync(MirrorCatalog.Repository, tag, cancellationToken), failure =>
            {
                complete = false;
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    LogPinFailed(logger, tag, failure.Host, failure.Kind);
                }
            });
            if (answer is not null)
            {
                all.Add(answer.Release);
            }
        }

        return (all, complete);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The pinned build {Tag} could not be asked for at {Host} ({Kind}); the build list goes without it this time")]
    private static partial void LogPinFailed(ILogger logger, string tag, string host, NetErrorKind kind);
}
