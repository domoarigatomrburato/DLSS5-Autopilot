using Autopilot.Core.Resources;

namespace Autopilot.Core.Components;

/// <summary>Where GitHub release data came from.</summary>
public enum ReleaseOrigin
{
    /// <summary>The API's answer, live or reused while fresh.</summary>
    Api,

    /// <summary>An API answer kept earlier, used because the live request failed.</summary>
    StaleCache,

    /// <summary>github.com's release pages, read because the API failed and nothing was kept.</summary>
    Pages,
}

/// <summary>Where release data came from, how old it is, and what the install says about it.</summary>
/// <param name="Origin">Where the data came from.</param>
/// <param name="Age">The answer's age: within the cache's freshness for the API, any age for a stale answer, none for the pages.</param>
public sealed record ReleaseSource(ReleaseOrigin Origin, TimeSpan Age)
{
    /// <summary>The words the install shows about this source; <see langword="null"/> for the API's own answer.</summary>
    public string? Notice => Origin switch
    {
        ReleaseOrigin.StaleCache => Texts.Format("ReleasesFromStaleCache", (long)Math.Max(0, Age.TotalHours)),
        ReleaseOrigin.Pages => Texts.Text("ReleasesFromPages"),
        _ => null,
    };
}

/// <summary>One release, and where it came from.</summary>
public sealed record ReleaseAnswer(GitHubRelease Release, ReleaseSource Source);

/// <summary>Releases newest first, and where they came from.</summary>
public sealed record ReleaseListAnswer(IReadOnlyList<GitHubRelease> Releases, ReleaseSource Source);
