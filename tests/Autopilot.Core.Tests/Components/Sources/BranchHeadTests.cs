using System.Net;
using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using static Autopilot.Core.Tests.Components.ComponentsBed;

namespace Autopilot.Core.Tests.Components.Sources;

/// <summary>A branch's head commit as the API answers it: kept like any API answer, and without one nothing is downloaded.</summary>
[TestClass]
public sealed class BranchHeadTests(TestContext testContext)
{
    [TestMethod]
    public async Task With_the_api_unreachable_a_kept_answer_of_any_age_is_used_and_said()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(VortHead, "lumenite-mainline-head.json").Unreachable(VortHead);
        _ = await bed.Resolve(ComponentIds.Vort);
        bed.Advance(30);

        ComponentRelease release = await bed.NextRun().ResolveAsync(ComponentIds.Vort, bed.Cancellation);

        Assert.AreEqual(Commit, release.Version.Label);
        Assert.AreEqual(ReleaseOrigin.StaleCache, release.Source!.Origin);
        Assert.AreEqual("GitHub could not be reached (rate limit or no connection); using the version list cached 30h ago.", release.Notice);
    }

    [TestMethod]
    public async Task With_the_api_unreachable_and_nothing_kept_nothing_is_downloaded_from_the_moving_branch()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Unreachable(LumeniteHead);

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Sources.ResolveLumeniteAsync(bed.Cancellation));

        Assert.AreEqual(ComponentFailure.BranchUnavailable, failure.Reason);
        StringAssert.StartsWith(failure.Message, "The newest commit of umar-afzaal/LumeniteFX (branch mainline) could not be read: ", StringComparison.Ordinal);
        Assert.HasCount(1, bed.Server.Requests, "the branch head only: no archive of a moving branch");
    }

    [TestMethod]
    public async Task A_branch_the_api_does_not_know_is_unavailable()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(VortHead, "rate-limited-403.json", HttpStatusCode.NotFound);

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Resolve(ComponentIds.Vort));

        Assert.AreEqual(ComponentFailure.BranchUnavailable, failure.Reason);
        StringAssert.Contains(failure.Message, "could not be read: HTTP 404.", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("[]")]
    [DataRow(/*lang=json,strict*/ """{"sha": 5}""")]
    [DataRow(/*lang=json,strict*/ """{"sha": "main"}""")]
    [DataRow(/*lang=json,strict*/ """{"sha": "A6D42FC9851B55FCB134AB8BB1199122FAFBDA13"}""")]
    public async Task An_answer_that_names_no_commit_is_not_release_data(string body)
    {
        using ComponentsBed bed = new(testContext);
        _ = bed.Server.Answer(VortHead, HttpStatusCode.OK, body);

        ReleaseDataException failure = await Assert.ThrowsAsync<ReleaseDataException>(() => bed.Resolve(ComponentIds.Vort));

        Assert.AreEqual(new Uri(VortHead), failure.Url);
    }
}
