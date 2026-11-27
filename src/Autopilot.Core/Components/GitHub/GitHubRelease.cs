using Autopilot.Core.Net;

namespace Autopilot.Core.Components.GitHub;

/// <summary>
/// One GitHub release. A flag or date that is <see langword="null"/> is not known: github.com's
/// release pages show none of them, so a caller that needs one decides without it (the feeder's
/// test-build rule falls back on the tag's own words).
/// </summary>
public sealed record GitHubRelease(string Tag, bool? Prerelease, bool? Draft, DateTimeOffset? PublishedAt, IReadOnlyList<GitHubAsset> Assets);

/// <summary>A release's file: its name, where it downloads from, and its SHA-256 when the API gives one.</summary>
public sealed record GitHubAsset(string Name, Uri Url, Sha256Verification? Digest);
