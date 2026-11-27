using System.Net;
using Autopilot.Core.Components;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components.GitHub;

[TestClass]
public sealed class ReleaseCacheTests(TestContext testContext)
{
    [TestMethod]
    public async Task A_fresh_answer_is_reused_without_a_request()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiList(5), HttpStatusCode.OK, ReleaseFixtures.Fixture("dxvk-releases.json"));

        _ = await bed.List(5);
        bed.Net.Advance(5 * 3600);
        ReleaseListAnswer again = await bed.List(5);

        Assert.HasCount(1, bed.Server.Requests);
        Assert.HasCount(5, again.Releases);
        Assert.AreEqual(new ReleaseSource(ReleaseOrigin.Api, TimeSpan.FromHours(5)), again.Source);
        Assert.IsNull(again.Source.Notice);
    }

    [TestMethod]
    public async Task When_the_api_is_rate_limited_an_old_answer_is_used_and_its_age_said()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiLatest, HttpStatusCode.OK, ReleaseFixtures.Fixture("dxvk-latest.json"));
        bed.RateLimited(ReleasesBed.ApiLatest);
        _ = await bed.Latest();
        bed.Net.Advance((30 * 3600) + 1800);

        ReleaseAnswer? stale = await bed.Latest();

        Assert.AreEqual("v3.2", stale?.Release.Tag);
        Assert.AreEqual(new ReleaseSource(ReleaseOrigin.StaleCache, TimeSpan.FromHours(30.5)), stale?.Source);
        Assert.AreEqual(
            "GitHub could not be reached (rate limit or no connection); using the version list cached 30h ago.",
            stale?.Source.Notice);
        Assert.AreEqual(0, bed.RequestsTo(ReleasesBed.LatestPage), "the pages are not asked when an answer was kept");
    }

    [TestMethod]
    public async Task When_the_api_asks_to_slow_down_an_old_answer_is_used()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiList(5), HttpStatusCode.OK, ReleaseFixtures.Fixture("dxvk-releases.json"));
        bed.SecondaryRateLimited(ReleasesBed.ApiList(5));
        _ = await bed.List(5);
        bed.Net.Advance(7 * 3600);

        ReleaseListAnswer stale = await bed.List(5);

        Assert.AreEqual(ReleaseOrigin.StaleCache, stale.Source.Origin);
        Assert.HasCount(5, stale.Releases);
    }

    [TestMethod]
    public async Task When_the_api_is_unreachable_an_old_answer_is_used()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiTag, HttpStatusCode.OK, ReleaseFixtures.Fixture("dxvk-latest.json"));
        bed.Unreachable(ReleasesBed.ApiTag);
        _ = await bed.ByTag("v3.2");
        bed.Net.Advance(3 * 24 * 3600);

        ReleaseAnswer? stale = await bed.ByTag("v3.2");

        Assert.AreEqual(new ReleaseSource(ReleaseOrigin.StaleCache, TimeSpan.FromDays(3)), stale?.Source);
        Assert.Contains("cached 72h ago", stale!.Source.Notice!);
    }

    [TestMethod]
    public async Task A_refusal_is_never_covered_by_a_kept_answer()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiLatest, HttpStatusCode.OK, ReleaseFixtures.Fixture("dxvk-latest.json"))
            .Redirect(ReleasesBed.ApiLatest, "https://evil.test/repos/doitsujin/dxvk/releases/latest");
        _ = await bed.Latest();
        bed.Net.Advance(7 * 3600);

        NetException failure = await Assert.ThrowsAsync<NetException>(bed.Latest);

        Assert.AreEqual(NetErrorKind.RedirectRefused, failure.Kind);
        Assert.AreEqual(0, bed.RequestsTo(ReleasesBed.LatestPage));
    }
}
