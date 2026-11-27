using System.Globalization;
using System.Text.RegularExpressions;

namespace Autopilot.Core.Components.GitHub;

/// <summary>A repository on GitHub, <c>owner/name</c>, and the addresses its release data lives at.</summary>
internal sealed partial record GitHubRepository(string Owner, string Name)
{
    private const string Site = "https://github.com/";

    /// <exception cref="ArgumentException"><paramref name="repository"/> is not <c>owner/name</c>.</exception>
    public static GitHubRepository Parse(string repository)
    {
        Match match = Shape().Match(repository);
        return match.Success && match.Groups["name"].Value is not ("." or "..")
            ? new(match.Groups["owner"].Value, match.Groups["name"].Value)
            : throw new ArgumentException($"'{repository}' is not a GitHub repository (owner/name)", nameof(repository));
    }

    /// <summary><c>api.github.com/repos/&lt;repo&gt;/releases</c> followed by <paramref name="rest"/>.</summary>
    public Uri Api(string rest) => new($"https://api.github.com/repos/{Owner}/{Name}/releases{rest}");

    public Uri ReleasesPage(int page) => Page("releases?page=" + page.ToString(CultureInfo.InvariantCulture));

    public Uri Latest => Page("releases/latest");

    public Uri ExpandedAssets(string tag) => Page("releases/expanded_assets/" + Uri.EscapeDataString(tag));

    public Uri LatestDownload(string assetName) => Page("releases/latest/download/" + Uri.EscapeDataString(assetName));

    /// <summary>An asset of the release tagged <paramref name="tag"/>, by GitHub's download address.</summary>
    public Uri ReleaseDownload(string tag, string assetName) =>
        Page($"releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(assetName)}");

    /// <summary><c>api.github.com/repos/&lt;repo&gt;/commits/&lt;branch&gt;</c>: the commit at the head of a branch.</summary>
    public Uri BranchHead(string branch) => new($"https://api.github.com/repos/{Owner}/{Name}/commits/{Uri.EscapeDataString(branch)}");

    /// <summary>The whole repository at one commit, as a zip.</summary>
    public Uri CommitArchive(string commit) => new($"https://codeload.github.com/{Owner}/{Name}/zip/{commit}");

    /// <summary>One file of the repository at one commit.</summary>
    public Uri RawFile(string commit, string path) => new($"https://raw.githubusercontent.com/{Owner}/{Name}/{commit}/{path}");

    /// <summary>The tag a github.com address names as one of this repository's releases (<c>/releases/tag/&lt;tag&gt;</c>), if it does.</summary>
    public string? TagIn(Uri url) => ReleasePath(url, "tag", 6) is [.., string tag] ? Uri.UnescapeDataString(tag) : null;

    /// <summary>The asset name a github.com address downloads from one of this repository's releases, if it does.</summary>
    public string? AssetIn(Uri url) => ReleasePath(url, "download", 7) is [.., string name] ? Uri.UnescapeDataString(name) : null;

    public override string ToString() => $"{Owner}/{Name}";

    private Uri Page(string rest) => new($"{Site}{Owner}/{Name}/{rest}");

    // github.com/<owner>/<name>/releases/<kind>/..., with every part present; owners and names ignore case on GitHub.
    private string[]? ReleasePath(Uri url, string kind, int parts)
    {
        string[] path = url.AbsolutePath.Split('/');
        return url.Scheme == Uri.UriSchemeHttps
            && string.Equals(url.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            && path.Length == parts
            && string.Equals(path[1], Owner, StringComparison.OrdinalIgnoreCase)
            && string.Equals(path[2], Name, StringComparison.OrdinalIgnoreCase)
            && path[3] == "releases"
            && path[4] == kind
            && Array.TrueForAll(path[5..], part => part.Length > 0)
            ? path
            : null;
    }

    [GeneratedRegex(@"^(?<owner>[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)/(?<name>[A-Za-z0-9._-]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
