using Autopilot.Core.Components.GitHub;

namespace Autopilot.Core.Tests.Components;

/// <summary>
/// Answers captured from GitHub for <c>doitsujin/dxvk</c> on 2026-11-27 (release notes trimmed from
/// the JSON, the list page trimmed to its releases), one <c>NIGos/dlss5-bridge</c> list with
/// pre-releases, and GitHub's rate-limit answers, written by hand. Captured the same day for the
/// feeder, ReShade and OptiScaler sources: reshade.me's home page (trimmed to its download block),
/// <c>crosire/reshade</c>'s tags, the feeder's and the OptiScaler forks' release lists (trimmed to
/// the fields read and four assets a release), and jlrouzies' sums file of v0.8.93. Captured the
/// same day for the other components, trimmed to the fields read: the newest release of
/// dlss5-bridge, neural-upstream, standalone-dlssnr and REFramework, DXVK v3.1.1 (with its
/// <c>native</c> sibling), the pinned Remix runtime, MFG unlock and ASI Loader releases,
/// GITC-Uplift's list (a draft and a pre-release added by hand on top) with v2.1.0's sums file, and
/// LumeniteFX's branch head.
/// </summary>
internal static class ReleaseFixtures
{
    /// <summary>The ten tags on the captured list page, newest first.</summary>
    public static readonly string[] PageTags = ["v3.2", "v3.1.1", "v3.1", "v3.0.2", "v3.0.1", "v3.0", "v2.7.1", "v2.7", "v2.6.2", "v2.6.1"];

    public static string Fixture(string name) => File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Components", "Fixtures", name));

    /// <summary>A list page with the captured DXVK page's markup around other tags.</summary>
    public static string ListPageWith(params string[] tags) => ListPageWith("dxvk-releases-page.html", "v3.2", tags);

    /// <summary>A captured list page with its first release's markup repeated for each of <paramref name="tags"/>, in place of <paramref name="placeholderTag"/>.</summary>
    public static string ListPageWith(string fixture, string placeholderTag, params string[] tags)
    {
        string page = Fixture(fixture);
        int first = page.IndexOf("  <section", StringComparison.Ordinal);
        int end = page.LastIndexOf("</section>", StringComparison.Ordinal) + "</section>".Length;
        string section = page[first..(page.IndexOf("</section>", first, StringComparison.Ordinal) + "</section>".Length)];
        return page[..first] + string.Concat(tags.Select(tag => section.Replace(placeholderTag, tag, StringComparison.Ordinal) + "\n")) + page[end..];
    }

    /// <summary>An asset fragment: the captured one for v3.2, the same markup under another tag otherwise.</summary>
    public static string ExpandedFor(string tag) => Fixture("dxvk-expanded-assets-v3.2.html").Replace("v3.2", tag, StringComparison.Ordinal);

    /// <summary>The <c>location</c> header of a captured redirect.</summary>
    public static string LocationIn(string fixture) =>
        Fixture(fixture).Split('\n').Single(line => line.StartsWith("location: ", StringComparison.Ordinal))["location: ".Length..];

    public static string[] Tags(IEnumerable<GitHubRelease> releases) => [.. releases.Select(release => release.Tag)];

    public static string[] AssetNames(GitHubRelease release) => AssetNames(release.Assets);

    public static string[] AssetNames(IEnumerable<GitHubAsset> assets) => [.. assets.Select(asset => asset.Name)];

    /// <summary>How many github.com asset pages <paramref name="server"/> was asked for.</summary>
    public static int AssetPageRequests(Net.FakeServer server) =>
        server.Requests.Count(request => request.Url.AbsolutePath.Contains("/expanded_assets/", StringComparison.Ordinal));
}
