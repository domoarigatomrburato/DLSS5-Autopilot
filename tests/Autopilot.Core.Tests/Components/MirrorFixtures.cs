using System.Text.Json.Nodes;

namespace Autopilot.Core.Tests.Components;

/// <summary>
/// Answers captured from <c>RankFTW/rhi-repo</c> on 2026-11-27: its API release list (100 releases,
/// trimmed to the fields the tool reads), its first release list page (trimmed to the releases), the
/// asset fragment of <c>renodx-dlss5-4.55</c>, and <c>NVIDIA/DLSS</c>'s latest-release redirect.
/// </summary>
internal static class MirrorFixtures
{
    /// <summary>The ten tags on the captured list page, newest first.</summary>
    public static readonly string[] PageTags =
    [
        "DLSS-Enabler-4.10.0.27", "dlssnr-310.9.16-nvidia", "DLSS-Enabler-4.10.0.23", "renodx-dlss-SF-26.1025.0009", "DLSS-Enabler-4.10.0.18",
        "streamline-2.14.16.0-v2", "renodx-dlss5-10.8.0", "renodx-dlss-SF-26.1016.1304", "dlssnr-310.9.16", "streamline-2.14.16.0",
    ];

    /// <summary>The digest the API and the asset fragment show for <c>renodx-dlss5_4.55.zip</c>.</summary>
    public const string Renodx455Digest = "15481c492db76682e9a88917e7f78897351ecf088bfae9bca74a0c5b74ddd033";

    private const string CapturedFragmentTag = "renodx-dlss5-4.55";

    /// <summary>The API's release list, without the releases tagged <paramref name="leftOut"/>.</summary>
    public static string ApiList(params string[] leftOut)
    {
        JsonArray releases = JsonNode.Parse(ReleaseFixtures.Fixture("rhi-repo-releases.json"))!.AsArray();
        foreach (JsonNode? release in releases.Where(release => leftOut.Contains((string?)release!["tag_name"])).ToList())
        {
            _ = releases.Remove(release);
        }

        return releases.ToJsonString();
    }

    /// <summary>
    /// A list of 100 releases that no longer reaches the pinned builds: the captured one without
    /// <c>renodx-dlss5-4.55</c> and <c>-4.60</c>, two newer releases of no family in front.
    /// </summary>
    public static string ApiListPastPins()
    {
        JsonArray releases = JsonNode.Parse(ApiList("renodx-dlss5-4.55", "renodx-dlss5-4.60"))!.AsArray();
        foreach (string tag in new[] { "streamline-2.15.0.0", "streamline-2.15.1.0" })
        {
            releases.Insert(0, JsonNode.Parse($$"""{"tag_name":"{{tag}}","assets":[{"name":"{{tag}}.zip","browser_download_url":"https://github.com/RankFTW/rhi-repo/releases/download/{{tag}}/{{tag}}.zip"}]}"""));
        }

        return releases.ToJsonString();
    }

    /// <summary>The API's answer for one release of the captured list, as <c>/releases/tags/&lt;tag&gt;</c> gives it.</summary>
    public static string ApiRelease(string tag) =>
        JsonNode.Parse(ReleaseFixtures.Fixture("rhi-repo-releases.json"))!.AsArray().Single(release => (string?)release!["tag_name"] == tag)!.ToJsonString();

    /// <summary>The captured list page, or its markup around other tags.</summary>
    public static string ListPage(params string[] tags)
    {
        string page = ReleaseFixtures.Fixture("rhi-repo-releases-page.html");
        if (tags.Length == 0)
        {
            return page;
        }

        int first = page.IndexOf("  <section", StringComparison.Ordinal);
        int end = page.LastIndexOf("</section>", StringComparison.Ordinal) + "</section>".Length;
        string section = page[first..(page.IndexOf("</section>", first, StringComparison.Ordinal) + "</section>".Length)];
        return page[..first] + string.Concat(tags.Select(tag => section.Replace(PageTags[0], tag, StringComparison.Ordinal) + "\n")) + page[end..];
    }

    /// <summary>
    /// An asset fragment: the captured one for <c>renodx-dlss5-4.55</c>, the same markup under another
    /// tag otherwise, whose zip is then named <c>&lt;tag&gt;.zip</c>.
    /// </summary>
    public static string ExpandedFor(string tag) =>
        ReleaseFixtures.Fixture("rhi-repo-expanded-assets-renodx-dlss5-4.55.html")
            .Replace(CapturedFragmentTag, tag, StringComparison.Ordinal)
            .Replace("renodx-dlss5_4.55", tag, StringComparison.Ordinal);

    /// <summary>The <c>location</c> of the captured <c>NVIDIA/DLSS</c> latest-release redirect.</summary>
    public static string NvidiaLatestLocation() =>
        ReleaseFixtures.Fixture("nvidia-dlss-latest-redirect.txt").Split('\n').Single(line => line.StartsWith("location: ", StringComparison.Ordinal))["location: ".Length..];
}
