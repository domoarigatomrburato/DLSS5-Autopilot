using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Autopilot.Core.Components;

/// <summary>
/// Release data read off github.com's own pages, by the anchors' addresses: a release list's tags,
/// and one release's assets. Links inside release notes are not the page's own.
/// </summary>
internal static class ReleaseHtml
{
    /// <summary>The release tags a <c>/releases?page=n</c> page lists, in its order, each once.</summary>
    public static IReadOnlyList<string> Tags(string html, Uri page, GitHubRepository repository) =>
        [.. Links(html, page).Select(repository.TagIn).OfType<string>().Distinct(StringComparer.Ordinal)];

    /// <summary>The assets an <c>expanded_assets/&lt;tag&gt;</c> fragment lists; the source archives are not assets.</summary>
    public static IReadOnlyList<GitHubAsset> Assets(string html, Uri fragment, GitHubRepository repository) =>
        [.. Links(html, fragment)
            .Select(url => (Url: url, Name: repository.AssetIn(url)))
            .Where(link => link.Name is not null)
            .DistinctBy(link => link.Name, StringComparer.Ordinal)
            .Select(link => new GitHubAsset(link.Name!, link.Url, Digest: null))];

    // Every anchor outside release notes, made absolute against the page it is on.
    private static List<Uri> Links(string html, Uri page)
    {
        using IDocument document = new HtmlParser().ParseDocument(html);
        List<Uri> links = [];
        foreach (IElement anchor in document.QuerySelectorAll("a[href]"))
        {
            if (anchor.Closest(".markdown-body") is null && Uri.TryCreate(page, anchor.GetAttribute("href"), out Uri? url))
            {
                links.Add(url);
            }
        }

        return links;
    }
}
