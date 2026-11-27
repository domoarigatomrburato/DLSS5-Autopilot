using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components.Sources;

/// <summary>What the release-based components share: turning a release's assets into downloads, and the shape refusals.</summary>
internal static class ReleaseAssets
{
    /// <summary>Why a file fetched by GitHub's redirect or by a guessed name is checked by its magic bytes only.</summary>
    public const string NoReleaseData = "taken with no release data, so no digest is known";

    /// <summary>The component from <paramref name="assets"/> of the answer's release, each checked as the catalog says.</summary>
    /// <exception cref="ComponentException">The publisher puts sums in every release and this one carries none.</exception>
    public static ComponentRelease Of(string id, ReleaseAnswer answer, IReadOnlyList<GitHubAsset> assets, IReadOnlyList<ExpectedFile> files)
    {
        CatalogEntry entry = ComponentCatalog.Of(id);
        return new(
            id,
            new(answer.Release.Tag),
            [.. assets.Select(asset => new ComponentDownload(asset.Name, asset.Url, entry.VerificationOf(answer.Release, asset)))],
            files,
            answer.Source);
    }

    /// <summary>
    /// The assets named, from the newest release, each also a file the downloads hold; with no
    /// release data, each by GitHub's <c>latest/download</c> redirect.
    /// </summary>
    /// <exception cref="ComponentException">The release lacks one: it is of another shape.</exception>
    public static async Task<ComponentRelease> ByNameAsync(GitHubReleases releases, string id, IReadOnlyList<string> names, CancellationToken cancellationToken) =>
        await LatestOrNoneAsync(releases, id, cancellationToken) is { } latest
            ? ByName(id, latest, names, [.. names.Select(name => new ExpectedFile(name))])
            : ByLatestRedirect(id, names);

    /// <summary>The assets named, from the answer's release, holding <paramref name="files"/>.</summary>
    /// <exception cref="ComponentException">The release lacks one, or the publisher puts sums in every release and this one carries none.</exception>
    public static ComponentRelease ByName(string id, ReleaseAnswer answer, IReadOnlyList<string> names, IReadOnlyList<ExpectedFile> files) =>
        Of(id, answer, Named(id, answer.Release, names), files);

    /// <summary>Every asset named, case ignored.</summary>
    /// <exception cref="ComponentException">One is missing: the release is of another shape.</exception>
    private static GitHubAsset[] Named(string id, GitHubRelease release, IReadOnlyList<string> names)
    {
        string[] missing = [.. names.Where(name => !release.Assets.Any(asset => Is(asset, name)))];
        return missing.Length == 0
            ? [.. names.Select(name => release.Assets.First(asset => Is(asset, name)))]
            : throw ComponentCatalog.Of(id).LayoutChanged(release.Tag, string.Join(", ", missing));
    }

    /// <summary>The first asset of <paramref name="release"/> that <paramref name="predicate"/> accepts.</summary>
    /// <exception cref="ComponentException">None does: the release is of another shape (<paramref name="what"/> names what was looked for).</exception>
    public static GitHubAsset First(string id, GitHubRelease release, Func<GitHubAsset, bool> predicate, string what) =>
        release.Assets.FirstOrDefault(predicate) ?? throw ComponentCatalog.Of(id).LayoutChanged(release.Tag, what);

    public static bool Is(GitHubAsset asset, string name) => string.Equals(asset.Name, name, StringComparison.OrdinalIgnoreCase);

    /// <summary>Each file by GitHub's <c>latest/download</c> redirect: the version is <c>latest</c>, which is never compared.</summary>
    public static ComponentRelease ByLatestRedirect(string id, IReadOnlyList<string> names)
    {
        CatalogEntry entry = ComponentCatalog.Of(id);
        GitHubRepository repository = entry.Repository;
        NoVerification check = entry.Unchecked(NoReleaseData);
        return new(
            id,
            ComponentVersion.Latest,
            [.. names.Select(name => new ComponentDownload(name, repository.LatestDownload(name), check))],
            [.. names.Select(name => new ExpectedFile(name))],
            null);
    }

    /// <summary>
    /// Drafts and pre-releases dropped, newest published first. From the pages, where no flag is
    /// known, a tag carrying a test word counts as a pre-release.
    /// </summary>
    public static IEnumerable<GitHubRelease> Published(IEnumerable<GitHubRelease> list) =>
        list.Where(release => release.Draft != true && !(release.Prerelease ?? new ComponentVersion(release.Tag).IsTestBuild))
            .OrderByDescending(release => release.PublishedAt ?? DateTimeOffset.MinValue);

    /// <summary>
    /// The component from the first published release of <paramref name="list"/> carrying an asset
    /// <paramref name="find"/> finds, newest published first.
    /// </summary>
    /// <exception cref="ComponentException">None carries it (<paramref name="asset"/> names it), or the publisher puts sums in every release and that one carries none.</exception>
    public static ComponentRelease FirstPublishedCarrying(
        string id,
        ReleaseListAnswer list,
        Func<GitHubRelease, GitHubAsset?> find,
        string asset,
        IReadOnlyList<ExpectedFile> files) =>
        FirstCarrying(Published(list.Releases), find) is var (release, found)
            ? Of(id, new(release, list.Source), [found], files)
            : throw ComponentCatalog.Of(id).NoneCarries(asset, list.Releases.Count > 0 ? list.Releases[0].Tag : null);

    /// <summary>The first of <paramref name="releases"/> carrying an asset <paramref name="find"/> finds, and that asset; <see langword="null"/> when none does.</summary>
    public static (GitHubRelease Release, GitHubAsset Asset)? FirstCarrying(IEnumerable<GitHubRelease> releases, Func<GitHubRelease, GitHubAsset?> find)
    {
        foreach (GitHubRelease release in releases)
        {
            if (find(release) is { } asset)
            {
                return (release, asset);
            }
        }

        return null;
    }

    /// <summary>The newest release; <see langword="null"/> when neither the API, a kept answer nor the pages could answer.</summary>
    public static Task<ReleaseAnswer?> LatestOrNoneAsync(GitHubReleases releases, string id, CancellationToken cancellationToken) =>
        NetFallback.OrNullAsync(LatestAsync(releases, id, cancellationToken));

    /// <summary>The newest release.</summary>
    /// <exception cref="ComponentException">The repository has no release.</exception>
    public static async Task<ReleaseAnswer> LatestAsync(GitHubReleases releases, string id, CancellationToken cancellationToken)
    {
        string repository = ComponentCatalog.Of(id).Source;
        return await releases.GetLatestAsync(repository, cancellationToken)
            ?? throw new ComponentException(ComponentFailure.NoRelease, Texts.Format("ComponentNoRelease", repository));
    }
}
