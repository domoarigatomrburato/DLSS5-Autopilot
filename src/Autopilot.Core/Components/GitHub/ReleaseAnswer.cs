namespace Autopilot.Core.Components.GitHub;

/// <summary>One release, and where it came from.</summary>
public sealed record ReleaseAnswer(GitHubRelease Release, ReleaseSource Source);

/// <summary>Releases newest first, and where they came from.</summary>
public sealed record ReleaseListAnswer(IReadOnlyList<GitHubRelease> Releases, ReleaseSource Source);
