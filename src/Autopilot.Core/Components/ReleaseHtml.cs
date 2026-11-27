using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Autopilot.Core.Net;

namespace Autopilot.Core.Components;

/// <summary>
/// Release data read off github.com's own pages, by the anchors' addresses: a release list's tags,
/// and one release's assets with the SHA-256 each row shows. Links inside release notes are not the
/// page's own.
/// </summary>
internal static class ReleaseHtml
{
    private const string DigestPrefix = "sha256:";

    /// <summary>The release tags a <c>/releases?page=n</c> page lists, in its order, each once.</summary>
    public static IReadOnlyList<string> Tags(string html, Uri page, GitHubRepository repository) =>
        [.. Links(html, page).Select(link => repository.TagIn(link.Url)).OfType<string>().Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The assets an <c>expanded_assets/&lt;tag&gt;</c> fragment lists, each with the digest its row
    /// shows, under the same rule as the API's; the source archives are not assets.
    /// </summary>
    public static IReadOnlyList<GitHubAsset> Assets(string html, Uri fragment, GitHubRepository repository) =>
        [.. Links(html, fragment)
            .Select(link => (link.Url, link.Anchor, Name: repository.AssetIn(link.Url)))
            .Where(link => link.Name is not null)
            .DistinctBy(link => link.Name, StringComparer.Ordinal)
            .Select(link => new GitHubAsset(link.Name!, link.Url, DigestOf(link.Anchor)))];

    // The row an asset's anchor sits in shows its digest as a text of its own ("sha256:<hex>").
    private static Sha256Verification? DigestOf(IElement anchor) =>
        anchor.Closest("li") is { } row
            ? ReleaseJson.Digest(row.QuerySelectorAll("*")
                .Where(element => element.ChildElementCount == 0)
                .Select(element => element.TextContent.Trim())
                .FirstOrDefault(text => text.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase)))
            : null;

    // Every anchor outside release notes, made absolute against the page it is on.
    private static List<(Uri Url, IElement Anchor)> Links(string html, Uri page)
    {
        using IDocument document = new HtmlParser().ParseDocument(html);
        HashSet<IElement> inNotes = [.. document.QuerySelectorAll(".markdown-body a[href], a.markdown-body[href]")];
        List<(Uri, IElement)> links = [];
        foreach (IElement anchor in document.QuerySelectorAll("a[href]"))
        {
            if (!inNotes.Contains(anchor) && Uri.TryCreate(page, anchor.GetAttribute("href"), out Uri? url))
            {
                links.Add((url, anchor));
            }
        }

        return links;
    }
}
