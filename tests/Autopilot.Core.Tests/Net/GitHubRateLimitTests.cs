using System.Globalization;
using System.Net;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class GitHubRateLimitTests(TestContext testContext)
{
    private const string Api = "https://api.github.com/repos/owner/repo/releases/latest";
    private const string OtherApi = "https://api.github.com/repos/owner/other/releases/latest";
    private const string Pages = "https://github.com/owner/repo/releases";

    [TestMethod]
    public async Task With_no_requests_left_the_api_is_not_asked_again_until_the_reset()
    {
        using TestBed bed = new(testContext);
        bed.Server
            .Answer(Api, HttpStatusCode.OK, "{}", ("x-ratelimit-remaining", "0"), ("x-ratelimit-reset", Epoch(bed, minutes: 40)))
            .Answer(OtherApi, HttpStatusCode.OK, "{}")
            .Answer(Pages, HttpStatusCode.OK, "<html>");

        TextAnswer first = await bed.GetText(Api);
        NetException limited = await bed.GetTextFails(OtherApi);
        TextAnswer pages = await bed.GetText(Pages);

        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);
        Assert.AreEqual(NetErrorKind.RateLimited, limited.Kind);
        Assert.AreEqual(bed.Start.AddMinutes(40), limited.RetryAt);
        Assert.AreEqual(0, bed.Server.RequestsTo(OtherApi));
        Assert.AreEqual("<html>", pages.Body, "only the API is held back");
    }

    [TestMethod]
    public async Task After_the_reset_the_api_is_asked_again()
    {
        using TestBed bed = new(testContext);
        bed.Server
            .Answer(Api, HttpStatusCode.OK, "{}", ("x-ratelimit-remaining", "0"), ("x-ratelimit-reset", Epoch(bed, minutes: 40)))
            .Answer(OtherApi, HttpStatusCode.OK, "{}");

        await bed.GetText(Api);
        bed.Advance(40 * 60);
        await bed.GetText(OtherApi);

        Assert.AreEqual(1, bed.Server.RequestsTo(OtherApi));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.Forbidden)]
    [DataRow(HttpStatusCode.TooManyRequests)]
    public async Task A_refusal_from_the_api_is_a_rate_limit_using_retry_after(HttpStatusCode status)
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Api, status, /*lang=json,strict*/ """{"message":"API rate limit exceeded"}""", ("Retry-After", "30"));

        NetException failure = await bed.GetTextFails(Api);

        Assert.AreEqual(NetErrorKind.RateLimited, failure.Kind);
        Assert.AreEqual(bed.Start.AddSeconds(30), failure.RetryAt);
        Assert.HasCount(1, bed.Server.Requests);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_refusal_from_the_api_falls_back_to_the_reset_header()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Api, HttpStatusCode.Forbidden, "{}", ("x-ratelimit-remaining", "0"), ("x-ratelimit-reset", Epoch(bed, minutes: 10)));

        NetException failure = await bed.GetTextFails(Api);

        Assert.AreEqual(bed.Start.AddMinutes(10), failure.RetryAt);
    }

    [TestMethod]
    public async Task A_refusal_from_the_api_without_headers_is_still_a_rate_limit()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Api, HttpStatusCode.TooManyRequests);

        NetException failure = await bed.GetTextFails(Api);

        Assert.AreEqual(NetErrorKind.RateLimited, failure.Kind);
        Assert.IsNull(failure.RetryAt);
    }

    [TestMethod]
    public async Task A_forbidden_answer_from_another_host_is_just_the_answer()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer("https://example.test/a", HttpStatusCode.Forbidden, "no");

        TextAnswer answer = await bed.GetText("https://example.test/a");

        Assert.AreEqual(HttpStatusCode.Forbidden, answer.StatusCode);
    }

    [TestMethod]
    public async Task A_download_from_the_api_is_held_back_too()
    {
        const string Asset = "https://api.github.com/repos/o/r/releases/assets/1";
        using TestBed bed = new(testContext);
        bed.Server.Answer(Asset, HttpStatusCode.OK, "asset", ("x-ratelimit-remaining", "0"), ("x-ratelimit-reset", Epoch(bed, minutes: 60)));

        await bed.GetText(Asset);
        NetException failure = await bed.StreamFails(Asset);

        Assert.AreEqual(NetErrorKind.RateLimited, failure.Kind);
        Assert.HasCount(1, bed.Server.Requests);
    }

    private static string Epoch(TestBed bed, int minutes) =>
        bed.Start.AddMinutes(minutes).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
}
