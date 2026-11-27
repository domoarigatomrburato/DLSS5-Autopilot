using System.Net;
using Autopilot.Core.Components;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components;

[TestClass]
public sealed class ReleasePageTests(TestContext testContext)
{
    private const string PagesNotice = "GitHub's API could not be reached; this release was read from github.com's release pages instead.";

    [TestMethod]
    public async Task A_release_by_tag_is_read_from_its_asset_page()
    {
        using ReleasesBed bed = new(testContext);
        bed.SecondaryRateLimited(ReleasesBed.ApiTag).Fragments("v3.2");

        ReleaseAnswer? answer = await bed.ByTag("v3.2");

        Assert.AreEqual("v3.2", answer?.Release.Tag);
        Assert.AreEqual("dxvk-3.2.tar.gz", Assert.ContainsSingle(answer!.Release.Assets).Name);
        Assert.AreEqual(PagesNotice, answer.Source.Notice);
    }

    [TestMethod]
    public async Task The_latest_release_is_found_by_one_head_with_no_api_request()
    {
        using ReleasesBed bed = new(testContext);
        bed.RateLimited(ReleasesBed.ApiList(5)).Pages();
        bed.Server.Redirect(ReleasesBed.LatestPage, LatestLocation());
        _ = await bed.List(5);
        int apiRequests = bed.RequestsToApi;

        ReleaseAnswer? answer = await bed.Latest();

        Assert.AreEqual(new ReleaseSource(ReleaseOrigin.Pages, TimeSpan.Zero), answer?.Source);
        Assert.AreEqual(apiRequests, bed.RequestsToApi, "the API is held back until its reset, so it is not asked");
        Assert.AreEqual(0, bed.RequestsTo(ReleasesBed.ApiLatest));
        CollectionAssert.AreEqual(new[] { HttpMethod.Head }, bed.MethodsTo(ReleasesBed.LatestPage));
        Assert.AreEqual(2, bed.RequestsTo(ReleasesBed.Expanded("v3.2")), "once for the list, once for latest");
    }

    [TestMethod]
    public async Task When_the_pages_fail_too_the_api_failure_is_reported()
    {
        using ReleasesBed bed = new(testContext);
        bed.RateLimited(ReleasesBed.ApiList(5)).Unreachable(ReleasesBed.ListPage(1));

        NetException failure = await Assert.ThrowsAsync<NetException>(() => bed.List(5));

        Assert.AreEqual(NetErrorKind.RateLimited, failure.Kind);
        Assert.AreEqual("api.github.com", failure.Host);
    }

    [TestMethod]
    public async Task When_an_asset_page_is_missing_the_api_failure_is_reported()
    {
        using ReleasesBed bed = new(testContext);
        bed.Unreachable(ReleasesBed.ApiTag);
        bed.Server.Answer(ReleasesBed.Expanded("v3.2"), HttpStatusCode.NotFound, "Not Found");

        NetException failure = await Assert.ThrowsAsync<NetException>(() => bed.ByTag("v3.2"));

        Assert.AreEqual("api.github.com", failure.Host);
    }

    [TestMethod]
    public async Task A_latest_redirect_that_names_no_tag_is_no_answer_from_the_pages()
    {
        using ReleasesBed bed = new(testContext);
        bed.Unreachable(ReleasesBed.ApiLatest);
        bed.Server.Redirect(ReleasesBed.LatestPage, "https://github.com/doitsujin/dxvk/releases");

        NetException failure = await Assert.ThrowsAsync<NetException>(bed.Latest);

        Assert.AreEqual("api.github.com", failure.Host);
        Assert.HasCount(2, bed.Server.Requests);
    }

    [TestMethod]
    public async Task A_latest_redirect_off_the_list_is_refused_not_covered()
    {
        using ReleasesBed bed = new(testContext);
        bed.Unreachable(ReleasesBed.ApiLatest);
        bed.Server.Redirect(ReleasesBed.LatestPage, "https://evil.test/doitsujin/dxvk/releases/tag/v3.2");

        NetException failure = await Assert.ThrowsAsync<NetException>(bed.Latest);

        Assert.AreEqual(NetErrorKind.RedirectRefused, failure.Kind);
        Assert.AreEqual("evil.test", failure.Host);
    }

    [TestMethod]
    public async Task A_refused_api_redirect_is_not_covered_by_the_pages()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Redirect(ReleasesBed.ApiList(5), "http://api.github.com/repos/doitsujin/dxvk/releases?per_page=5");

        NetException failure = await Assert.ThrowsAsync<NetException>(() => bed.List(5));

        Assert.AreEqual(new RedirectRefusedDetail(RedirectProblem.Scheme, 1), failure.Detail);
        Assert.AreEqual(0, bed.RequestsTo(ReleasesBed.ListPage(1)));
    }

    // The 302 captured from github.com/doitsujin/dxvk/releases/latest.
    private static string LatestLocation() =>
        ReleaseFixtures.Fixture("dxvk-latest-redirect.txt").Split('\n').Single(line => line.StartsWith("location: ", StringComparison.Ordinal))["location: ".Length..];
}
