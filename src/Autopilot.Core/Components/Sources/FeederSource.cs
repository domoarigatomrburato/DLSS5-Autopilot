using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;
using Autopilot.Core.Resources;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components.Sources;

/// <summary>
/// DLSS5-Feeder builds, resolved once per choice per run from the 15 newest releases. The
/// author publishes test builds with GitHub's pre-release flag off, so a test build is also told
/// by its tag's own words, and "newest release" never lands on one while a release exists.
/// </summary>
public sealed partial class FeederSource(GitHubReleases releases, ILogger<FeederSource> logger)
{
    /// <summary>How many of the newest releases are read, and the only ones an exact tag is looked for in.</summary>
    public const int ListedReleases = 15;

    private static readonly GitHubRepository _repository = ComponentCatalog.Of(ComponentIds.Feeder).Repository;

    private readonly ResolutionMemory<FeederChoice, FeederRelease> _memory = new();
    private readonly FeederPages _pages = new(releases, logger);

    /// <summary>The release <paramref name="choice"/> names; the same answer for the rest of the run once found.</summary>
    /// <exception cref="ComponentException">The tag is not among the 15 newest, there is no release, or the release carries no feeder files.</exception>
    /// <exception cref="NetException">Neither GitHub's API, nor a kept answer, nor the pages could answer.</exception>
    /// <exception cref="ReleaseDataException">The API's answer is not release data.</exception>
    public Task<FeederRelease> ResolveAsync(FeederChoice choice, CancellationToken cancellationToken) =>
        _memory.GetAsync(choice, token => ResolveOnceAsync(choice, token), cancellationToken);

    /// <summary>A test build: flagged a pre-release by GitHub, or carrying a test word in its tag.</summary>
    internal static bool IsTestBuild(GitHubRelease release) => release.Prerelease == true || new ComponentVersion(release.Tag).IsTestBuild;

    private async Task<FeederRelease> ResolveOnceAsync(FeederChoice choice, CancellationToken cancellationToken)
    {
        // Each choice picks from the same list: it is read once per run.
        ReleaseListAnswer list = await releases.GetListOnceAsync(_repository, ListedReleases, cancellationToken);
        GitHubRelease[] published = [.. list.Releases.Where(release => release.Draft != true)];
        (GitHubRelease release, ReleaseSource source, string? notice) = choice.Pick switch
        {
            FeederPick.ExactTag => (Exact(published, choice.Tag!), list.Source, null),
            FeederPick.NewestPrerelease => (Newest(published), list.Source, null),
            _ => await NewestReleaseAsync(published, list.Source, cancellationToken),
        };
        var resolved = FeederRelease.Of(release, source, notice);
        if (logger.IsEnabled(LogLevel.Information))
        {
            LogResolved(logger, release.Tag, choice.Pick, resolved.Package?.Name ?? "loose files");
        }

        return resolved;
    }

    private static GitHubRelease Exact(GitHubRelease[] published, string tag) =>
        Array.Find(published, release => release.Tag == tag)
        ?? throw new ComponentException(ComponentFailure.TagNotListed, Texts.Format("FeederTagNotListed", tag, ListedReleases));

    private static GitHubRelease Newest(GitHubRelease[] published) =>
        published.Length > 0 ? published[0] : throw new ComponentException(ComponentFailure.NoRelease, Texts.Text("FeederNoRelease"));

    private async Task<(GitHubRelease Release, ReleaseSource Source, string? Notice)> NewestReleaseAsync(
        GitHubRelease[] published,
        ReleaseSource listSource,
        CancellationToken cancellationToken)
    {
        if (Array.Find(published, release => !IsTestBuild(release)) is { } stable)
        {
            return (stable, listSource, null);
        }

        // Only test builds among the 15: the pages go back further, by tag alone.
        if (await _pages.NewestReleaseAsync(cancellationToken) is { } walked)
        {
            return (walked.Release, walked.Source, null);
        }

        // Every source answered a test build: GitHub's own "latest", else the newest of the list,
        // installed and said so rather than nothing installed.
        ReleaseAnswer? latest = await releases.GetLatestAsync(_repository, cancellationToken);
        (GitHubRelease release, ReleaseSource source) = latest is not null ? (latest.Release, latest.Source) : (Newest(published), listSource);
        return (release, source, IsTestBuild(release) ? Texts.Format("FeederEveryReleaseTest", release.Tag) : null);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "DLSS5-Feeder {Tag} ({Pick}): {Files}")]
    private static partial void LogResolved(ILogger logger, string tag, FeederPick pick, string files);
}

/// <summary>Which feeder build the person picked under "feeder build".</summary>
public enum FeederPick
{
    /// <summary>The newest release that is not a test build.</summary>
    NewestRelease,

    /// <summary>The newest build of any kind that is not a draft.</summary>
    NewestPrerelease,

    /// <summary>One tag, among the 15 newest releases.</summary>
    ExactTag,
}

/// <summary>A feeder build choice: one of the two "newest" picks, or an exact tag.</summary>
public sealed record FeederChoice
{
    private FeederChoice(FeederPick pick, string? tag)
    {
        Pick = pick;
        Tag = tag;
    }

    /// <summary>The default: the newest release that is not a test build.</summary>
    public static FeederChoice NewestRelease { get; } = new(FeederPick.NewestRelease, null);

    /// <summary>The newest build of any kind that is not a draft.</summary>
    public static FeederChoice NewestPrerelease { get; } = new(FeederPick.NewestPrerelease, null);

    public FeederPick Pick { get; }

    /// <summary>The exact tag; <see langword="null"/> for the "newest" picks.</summary>
    public string? Tag { get; }

    /// <summary>One release by its exact tag.</summary>
    /// <exception cref="ArgumentException">The tag is empty.</exception>
    public static FeederChoice Exact(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return new(FeederPick.ExactTag, tag);
    }
}

/// <summary>
/// A feeder release and the files to take from it: one zip whose name contains "feeder" (from
/// 0.10.0), else the loose files the release carries.
/// </summary>
/// <param name="Tag">The release's tag.</param>
/// <param name="Package">The zip holding every file; <see langword="null"/> for a release of loose files.</param>
/// <param name="LooseFiles">The feeder files published loose; none when <paramref name="Package"/> is set.</param>
/// <param name="Source">Where the release data came from.</param>
/// <param name="Notice">"Every recent release is a test build", when the default pick had to install one.</param>
public sealed record FeederRelease(string Tag, GitHubAsset? Package, IReadOnlyList<GitHubAsset> LooseFiles, ReleaseSource Source, string? Notice)
{
    /// <summary>The files a feeder release publishes loose, before 0.10.0 put them in one zip.</summary>
    public static IReadOnlyList<string> LooseNames { get; } =
        ["dlss5-feed.addon64", "dlss5-feed.addon32", "dlss5-feed-helper.addon64", "dlss5-feed-host64.exe", "DLSS5_Feed.fx"];

    public ComponentVersion Version => new(Tag);

    /// <summary>Whether this is a test build by its tag's own words (the flag is not kept here).</summary>
    public bool IsTestBuild => Version.IsTestBuild;

    /// <summary>
    /// How a file of this release proves itself: the SHA-256 GitHub's API gives for it, else
    /// nothing beyond the magic bytes (the release pages show no digest, and the feeder publishes no sums).
    /// </summary>
    public static Verification VerificationOf(GitHubAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return ComponentCatalog.Of(ComponentIds.Feeder).DigestOr(asset);
    }

    /// <exception cref="ComponentException">The release carries no feeder files.</exception>
    internal static FeederRelease Of(GitHubRelease release, ReleaseSource source, string? notice)
    {
        GitHubAsset? package = release.Assets.FirstOrDefault(asset =>
            asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && asset.Name.Contains("feeder", StringComparison.OrdinalIgnoreCase));
        GitHubAsset[] loose = package is null
            ? [.. release.Assets.Where(asset => LooseNames.Contains(asset.Name, StringComparer.OrdinalIgnoreCase))]
            : [];
        return package is not null || loose.Length > 0
            ? new(release.Tag, package, loose, source, notice)
            : throw new ComponentException(ComponentFailure.NoFiles, Texts.Format("FeederNoFiles", release.Tag));
    }
}
