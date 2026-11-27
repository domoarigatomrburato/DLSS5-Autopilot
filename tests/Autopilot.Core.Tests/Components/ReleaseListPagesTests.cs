using System.Net;
using Autopilot.Core.Components;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components;

[TestClass]
public sealed class ReleaseListPagesTests(TestContext testContext)
{
    private const string PagesNotice = "GitHub's API could not be reached; this release was read from github.com's release pages instead.";

    [TestMethod]
    public async Task With_the_api_down_and_nothing_kept_the_list_is_read_from_the_pages()
    {
        using ReleasesBed bed = new(testContext);
        bed.RateLimited(ReleasesBed.ApiList(5)).Pages();

        ReleaseListAnswer answer = await bed.List(5);

        CollectionAssert.AreEqual(ReleaseFixtures.PageTags[..5], ReleaseFixtures.Tags(answer.Releases));
        Assert.AreEqual(new ReleaseSource(ReleaseOrigin.Pages, TimeSpan.Zero), answer.Source);
        Assert.AreEqual(PagesNotice, answer.Source.Notice);
        Assert.AreEqual(0, bed.RequestsTo(ReleasesBed.ListPage(2)), "one page held enough tags");
        Assert.AreEqual(5, bed.AssetPageRequests);
        Assert.AreEqual(1, bed.RequestsTo(ReleasesBed.Expanded("v3.0.1")));
    }

    [TestMethod]
    public async Task A_release_from_the_pages_has_its_assets_and_no_flags()
    {
        using ReleasesBed bed = new(testContext);
        bed.RateLimited(ReleasesBed.ApiList(5)).Pages();

        GitHubRelease newest = (await bed.List(5)).Releases[0];

        Assert.IsNull(newest.Prerelease, "the pages show no flags");
        Assert.IsNull(newest.Draft);
        Assert.IsNull(newest.PublishedAt);
        GitHubAsset asset = Assert.ContainsSingle(newest.Assets, "the source archives are not assets");
        Assert.AreEqual(new GitHubAsset("dxvk-3.2.tar.gz", new("https://github.com/doitsujin/dxvk/releases/download/v3.2/dxvk-3.2.tar.gz"), null), asset);
    }

    [TestMethod]
    public async Task A_link_to_a_release_inside_release_notes_is_not_a_release_of_the_list()
    {
        using ReleasesBed bed = new(testContext);
        bed.Unreachable(ReleasesBed.ApiList(10)).Pages();

        ReleaseListAnswer answer = await bed.List(10);

        CollectionAssert.AreEqual(ReleaseFixtures.PageTags, ReleaseFixtures.Tags(answer.Releases));
    }

    [TestMethod]
    public async Task The_pages_list_stops_at_twelve_releases()
    {
        using ReleasesBed bed = new(testContext);
        string[] older = ["v2.6", "v2.5.3", "v2.5.2", "v2.5.1", "v2.5", "v2.4.1", "v2.4", "v2.3.1", "v2.3", "v2.2"];
        bed.Unreachable(ReleasesBed.ApiList(100)).Pages().Fragments(older);
        bed.Server.Answer(ReleasesBed.ListPage(2), HttpStatusCode.OK, ReleaseFixtures.ListPageWith(older));

        ReleaseListAnswer answer = await bed.List(100);

        CollectionAssert.AreEqual(ReleaseFixtures.PageTags.Concat(older[..2]).ToArray(), ReleaseFixtures.Tags(answer.Releases));
        Assert.AreEqual(0, bed.RequestsTo(ReleasesBed.ListPage(3)));
        Assert.AreEqual(0, bed.RequestsTo(ReleasesBed.Expanded("v2.5.2")), "no asset page past the twelfth release");
        Assert.AreEqual(12, bed.AssetPageRequests);
    }

    [TestMethod]
    public async Task The_pages_list_reads_four_pages_at_most()
    {
        using ReleasesBed bed = new(testContext);
        bed.Unreachable(ReleasesBed.ApiList(100));
        for (int page = 1; page <= 5; page++)
        {
            string[] tags = [$"v{page}.1", $"v{page}.0"];
            bed.Server.Answer(ReleasesBed.ListPage(page), HttpStatusCode.OK, ReleaseFixtures.ListPageWith(tags));
            bed.Fragments(tags);
        }

        ReleaseListAnswer answer = await bed.List(100);

        Assert.HasCount(8, answer.Releases);
        Assert.AreEqual(1, bed.RequestsTo(ReleasesBed.ListPage(4)));
        Assert.AreEqual(0, bed.RequestsTo(ReleasesBed.ListPage(5)));
    }

    [TestMethod]
    public async Task A_page_that_adds_no_release_ends_the_list()
    {
        using ReleasesBed bed = new(testContext);
        bed.Unreachable(ReleasesBed.ApiList(100)).Pages();
        bed.Server.Answer(ReleasesBed.ListPage(2), HttpStatusCode.OK, ReleaseFixtures.Fixture("dxvk-releases-page.html"));

        ReleaseListAnswer answer = await bed.List(100);

        Assert.HasCount(10, answer.Releases);
        Assert.AreEqual(0, bed.RequestsTo(ReleasesBed.ListPage(3)));
    }

    [TestMethod]
    public async Task A_later_page_that_fails_ends_the_list_with_what_was_read()
    {
        using ReleasesBed bed = new(testContext);
        bed.Unreachable(ReleasesBed.ApiList(100)).Pages().Unreachable(ReleasesBed.ListPage(2));

        ReleaseListAnswer answer = await bed.List(100);

        Assert.HasCount(10, answer.Releases);
        Assert.AreEqual(ReleaseOrigin.Pages, answer.Source.Origin);
    }

    [TestMethod]
    public async Task An_asset_page_redirected_off_the_list_is_refused_not_covered()
    {
        using ReleasesBed bed = new(testContext);
        bed.Unreachable(ReleasesBed.ApiList(5)).Fragments("v3.2", "v3.1.1");
        bed.Server.Answer(ReleasesBed.ListPage(1), HttpStatusCode.OK, ReleaseFixtures.Fixture("dxvk-releases-page.html"))
            .Redirect(ReleasesBed.Expanded("v3.1"), "https://evil.test/assets");

        NetException failure = await Assert.ThrowsAsync<NetException>(() => bed.List(5));

        Assert.AreEqual(NetErrorKind.RedirectRefused, failure.Kind);
    }
}
