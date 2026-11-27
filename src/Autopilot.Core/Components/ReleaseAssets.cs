using Autopilot.Core.Net;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components;

/// <summary>What the release-based components share: turning a release's assets into downloads, and the shape refusals.</summary>
internal static class ReleaseAssets
{
    /// <summary>Why a file fetched by GitHub's redirect or by a guessed name is checked by its magic bytes only.</summary>
    public const string NoReleaseData = "taken with no release data, so no digest is known";

    /// <summary>The component from <paramref name="assets"/> of the answer's release, each checked as the catalog says.</summary>
    /// <exception cref="ComponentException">The publisher puts sums in every release and this one carries none.</exception>
    public static ComponentRelease Of(string id, ReleaseAnswer answer, IReadOnlyList<GitHubAsset> assets, IReadOnlyList<ExpectedFile> files) =>
        new(
            id,
            new(answer.Release.Tag),
            [.. assets.Select(asset => new ComponentDownload(asset.Name, asset.Url, ComponentCatalog.VerificationOf(id, answer.Release, asset)))],
            files,
            answer.Source);

    /// <summary>Every asset named, case ignored.</summary>
    /// <exception cref="ComponentException">One is missing: the release is of another shape.</exception>
    public static GitHubAsset[] Named(string id, GitHubRelease release, IReadOnlyList<string> names)
    {
        string[] missing = [.. names.Where(name => !release.Assets.Any(asset => Is(asset, name)))];
        return missing.Length == 0
            ? [.. names.Select(name => release.Assets.First(asset => Is(asset, name)))]
            : throw LayoutChanged(id, release.Tag, string.Join(", ", missing));
    }

    public static bool Is(GitHubAsset asset, string name) => string.Equals(asset.Name, name, StringComparison.OrdinalIgnoreCase);

    /// <summary>Each file by GitHub's <c>latest/download</c> redirect: the version is <c>latest</c>, which is never compared.</summary>
    public static ComponentRelease ByLatestRedirect(string id, IReadOnlyList<string> names)
    {
        string repository = ComponentCatalog.Of(id).Source;
        NoVerification check = ComponentCatalog.Unchecked(id, NoReleaseData);
        return new(
            id,
            ComponentVersion.Latest,
            [.. names.Select(name => new ComponentDownload(name, GitHubReleases.LatestDownloadOf(repository, name).Url, check))],
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

    /// <summary>The newest release; <see langword="null"/> when neither the API, a kept answer nor the pages could answer.</summary>
    public static async Task<ReleaseAnswer?> LatestOrNoneAsync(GitHubReleases releases, string id, CancellationToken cancellationToken)
    {
        try
        {
            return await LatestAsync(releases, id, cancellationToken);
        }
        catch (NetException failure) when (!failure.IsRefusal)
        {
            return null;
        }
    }

    /// <summary>The newest release.</summary>
    /// <exception cref="ComponentException">The repository has no release.</exception>
    public static async Task<ReleaseAnswer> LatestAsync(GitHubReleases releases, string id, CancellationToken cancellationToken)
    {
        string repository = ComponentCatalog.Of(id).Source;
        return await releases.GetLatestAsync(repository, cancellationToken)
            ?? throw new ComponentException(ComponentFailure.NoRelease, Texts.Format("ComponentNoRelease", repository));
    }

    public static ComponentException LayoutChanged(string id, string tag, string missing) =>
        new(ComponentFailure.LayoutChanged, Texts.Format("ComponentLayoutChanged", ComponentCatalog.Of(id).Source, tag, missing));

    public static ComponentException NoneCarries(string id, string asset, IReadOnlyList<GitHubRelease> list) =>
        new(ComponentFailure.LayoutChanged, Texts.Format("ComponentNoneCarries", ComponentCatalog.Of(id).Source, asset, list.Count > 0 ? list[0].Tag : "none"));
}
