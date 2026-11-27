using System.Net;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Tests.Net;
using Microsoft.Extensions.Logging.Testing;

namespace Autopilot.Core.Tests.Components.Mirror;

/// <summary><see cref="MirrorCatalog"/> and <see cref="NvidiaRuntimes"/> on the network test bed, one attempt per request, answering from <see cref="MirrorFixtures"/>.</summary>
internal sealed class MirrorBed : IDisposable
{
    public static readonly string Api = ComponentsBed.ApiList(ComponentRepositories.Mirror, GitHubReleases.MaxListCount);
    public static readonly string NvidiaLatest = ComponentsBed.LatestPage(ComponentRepositories.Nvidia);

    public MirrorBed(TestContext context)
    {
        Cancellation = context.CancellationToken;
        Net = new(context, TestBed.SingleAttempt);
        GitHubReleases releases = new(Net.Client, Net.Time, ReleasesLog);
        Nvidia = new(releases, NvidiaLog);
        Catalog = new(releases, Nvidia, Log);
    }

    public TestBed Net { get; }

    public FakeServer Server => Net.Server;

    public FakeLogger<MirrorCatalog> Log { get; } = new();

    public FakeLogger<NvidiaRuntimes> NvidiaLog { get; } = new();

    public FakeLogger<GitHubReleases> ReleasesLog { get; } = new();

    public NvidiaRuntimes Nvidia { get; }

    public MirrorCatalog Catalog { get; }

    public CancellationToken Cancellation { get; }

    public static string ListPage(int page) => ComponentsBed.ListPage(ComponentRepositories.Mirror, page);

    public static string ApiTag(string tag) => ComponentsBed.Api(ComponentRepositories.Mirror, "/tags/" + tag);

    public static string Expanded(string tag) => ComponentsBed.Expanded(ComponentRepositories.Mirror, tag);

    /// <summary>The API answers the captured list, without the releases tagged <paramref name="leftOut"/>.</summary>
    public MirrorBed ApiAnswers(params string[] leftOut)
    {
        Server.Answer(Api, HttpStatusCode.OK, MirrorFixtures.ApiList(leftOut));
        return this;
    }

    /// <summary>The API answers <c>/releases/tags/&lt;tag&gt;</c> with the captured release.</summary>
    public MirrorBed ApiHasTag(string tag)
    {
        Server.Answer(ApiTag(tag), HttpStatusCode.OK, MirrorFixtures.ApiRelease(tag));
        return this;
    }

    /// <summary>The API answers <c>/releases/tags/&lt;tag&gt;</c> with "not found".</summary>
    public MirrorBed ApiLacksTag(string tag)
    {
        Server.Answer(ApiTag(tag), HttpStatusCode.NotFound, /*lang=json,strict*/ """{"message":"Not Found"}""");
        return this;
    }

    public MirrorBed Unreachable(string url)
    {
        _ = Server.Unreachable(url);
        return this;
    }

    /// <summary>NVIDIA's latest-release redirect points at the captured tag, <c>v310.9.16</c>.</summary>
    public MirrorBed NvidiaRedirects()
    {
        Server.Redirect(NvidiaLatest, MirrorFixtures.NvidiaLatestLocation());
        return this;
    }

    /// <summary>The list pages, in order, each with these tags; the page after the last answers 404.</summary>
    public MirrorBed Pages(params string[][] pages)
    {
        for (int page = 0; page < pages.Length; page++)
        {
            Server.Answer(ListPage(page + 1), HttpStatusCode.OK, page == 0 && pages[0] == MirrorFixtures.PageTags ? MirrorFixtures.ListPage() : MirrorFixtures.ListPage(pages[page]));
            _ = Fragments(pages[page]);
        }

        Server.Answer(ListPage(pages.Length + 1), HttpStatusCode.NotFound, "Not Found");
        return this;
    }

    /// <summary>An asset fragment for each tag: the captured one for <c>renodx-dlss5-4.55</c>.</summary>
    public MirrorBed Fragments(params IEnumerable<string> tags)
    {
        foreach (string tag in tags)
        {
            Server.Answer(Expanded(tag), HttpStatusCode.OK, MirrorFixtures.ExpandedFor(tag));
        }

        return this;
    }

    public Task<BuildCatalog> Get() => Catalog.GetAsync(Cancellation);

    public int RequestsTo(string url) => Server.RequestsTo(url);

    public void Dispose() => Net.Dispose();

    /// <summary>
    /// A catalog from the captured API list and NVIDIA's captured tag, without the releases tagged
    /// <paramref name="leftOut"/>; a pinned build left out is unknown to the API by its tag too.
    /// </summary>
    public static async Task<BuildCatalog> Captured(TestContext context, params string[] leftOut)
    {
        using MirrorBed bed = new(context);
        foreach (string pin in leftOut.Where(tag => tag.StartsWith("renodx-dlss5-4.", StringComparison.Ordinal)))
        {
            _ = bed.ApiLacksTag(pin);
        }

        return await bed.ApiAnswers(leftOut).NvidiaRedirects().Get();
    }

    /// <summary>The labels of a family, in order.</summary>
    public static string Labels(BuildCatalog catalog, BuildFamily family) => Labels(catalog.Entries(family));

    /// <summary>The entries' labels, in order, joined by commas.</summary>
    public static string Labels(IEnumerable<BuildEntry> entries) => string.Join(", ", entries.Select(entry => entry.Label));
}
