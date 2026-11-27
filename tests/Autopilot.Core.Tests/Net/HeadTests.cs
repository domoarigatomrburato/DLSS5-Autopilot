using System.Globalization;
using System.Net;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class HeadTests(TestContext testContext)
{
    private const string Latest = "https://github.com/owner/repo/releases/latest";
    private const string Tag = "https://github.com/owner/repo/releases/tag/v1.2.3";
    private const string Api = "https://api.github.com/repos/owner/repo/releases/latest";

    [TestMethod]
    public async Task A_redirect_reports_where_it_points_without_following_it()
    {
        using TestBed bed = new(testContext);
        bed.Server.Redirect(Latest, Tag).Answer(Tag, HttpStatusCode.OK, "<html>a whole release page</html>");

        HeadAnswer answer = await bed.Head(Latest);

        Assert.AreEqual(HttpStatusCode.Found, answer.StatusCode);
        Assert.AreEqual(new Uri(Tag), answer.Location);
        Assert.HasCount(1, bed.Server.Requests);
        Assert.AreEqual(HttpMethod.Head, bed.Server.Requests[0].Method);
        Assert.AreEqual(0, bed.Server.RequestsTo(Tag));
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_relative_location_is_made_absolute_against_the_asked_address()
    {
        using TestBed bed = new(testContext);
        bed.Server.Redirect(Latest, "tag/v2.0.0", HttpStatusCode.MovedPermanently);

        HeadAnswer answer = await bed.Head(Latest);

        Assert.AreEqual(new Uri("https://github.com/owner/repo/releases/tag/v2.0.0"), answer.Location);
    }

    [TestMethod]
    public async Task An_answer_that_is_not_a_redirect_has_no_location()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Latest, HttpStatusCode.OK, "", ("Location", Tag));

        HeadAnswer answer = await bed.Head(Latest);

        Assert.AreEqual(HttpStatusCode.OK, answer.StatusCode);
        Assert.IsNull(answer.Location);
    }

    [TestMethod]
    public async Task A_location_on_http_is_refused_as_a_redirect_hop_would_be()
    {
        using TestBed bed = new(testContext);
        bed.Server.Redirect(Latest, "http://github.com/owner/repo/releases/tag/v1");

        NetException failure = await bed.HeadFails(Latest);

        Assert.AreEqual(NetErrorKind.RedirectRefused, failure.Kind);
        Assert.AreEqual(new RedirectRefusedDetail(RedirectProblem.Scheme, 1), failure.Detail);
        Assert.HasCount(1, bed.Server.Requests, "a refused location is never retried");
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_location_on_a_host_off_the_list_is_refused_as_a_redirect_hop_would_be()
    {
        using TestBed bed = new(testContext);
        bed.Server.Redirect(Latest, "https://evil.test/owner/repo/releases/tag/v1");

        NetException failure = await bed.HeadFails(Latest);

        Assert.AreEqual(NetErrorKind.RedirectRefused, failure.Kind);
        Assert.AreEqual(new RedirectRefusedDetail(RedirectProblem.Host, 1), failure.Detail);
        Assert.AreEqual("evil.test", failure.Host);
        Assert.HasCount(1, bed.Server.Requests);
    }

    [TestMethod]
    public async Task An_address_off_the_list_is_refused_without_a_request()
    {
        using TestBed bed = new(testContext);

        NetException failure = await bed.HeadFails("https://evil.test/latest");

        Assert.AreEqual(NetErrorKind.HostNotAllowed, failure.Kind);
        Assert.IsEmpty(bed.Server.Requests);
    }

    [TestMethod]
    public async Task An_address_that_is_not_https_is_refused_without_a_request()
    {
        using TestBed bed = new(testContext);

        NetException failure = await bed.HeadFails("http://github.com/owner/repo/releases/latest");

        Assert.AreEqual(NetErrorKind.SchemeRefused, failure.Kind);
        Assert.IsEmpty(bed.Server.Requests);
    }

    [TestMethod]
    public async Task A_server_that_hiccups_is_asked_again_like_a_text_request()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Latest, HttpStatusCode.ServiceUnavailable, "busy").Redirect(Latest, Tag);

        Task<HeadAnswer> pending = bed.Head(Latest);
        bed.Advance(1);
        HeadAnswer answer = await pending;

        Assert.AreEqual(new Uri(Tag), answer.Location);
        CollectionAssert.AreEqual(new[] { 0.0, 1 }, bed.RequestSeconds);
        Assert.IsTrue(bed.Server.Requests.TrueForAll(request => request.Method == HttpMethod.Head));
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_server_that_keeps_failing_is_reported_after_three_attempts()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Latest, HttpStatusCode.BadGateway);

        Task<NetException> pending = bed.HeadFails(Latest);
        bed.Advance(1, 4);
        NetException failure = await pending;

        Assert.AreEqual(new HttpStatusDetail(HttpStatusCode.BadGateway), failure.Detail);
        Assert.HasCount(3, bed.Server.Requests);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_refusal_from_the_api_is_a_rate_limit_that_holds_the_api_back()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Api, HttpStatusCode.TooManyRequests, "", ("Retry-After", "30"));

        NetException failure = await bed.HeadFails(Api);

        Assert.AreEqual(NetErrorKind.RateLimited, failure.Kind);
        Assert.AreEqual(bed.Start.AddSeconds(30), Assert.IsInstanceOfType<RateLimitedDetail>(failure.Detail).RetryAt);
        Assert.HasCount(1, bed.Server.Requests);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_refusal_of_a_head_holds_the_api_back_for_every_request()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Api, HttpStatusCode.Forbidden);

        _ = await bed.HeadFails(Api);
        NetException held = await bed.GetTextFails(Api);

        Assert.AreEqual(NetErrorKind.RateLimited, held.Kind);
        Assert.HasCount(1, bed.Server.Requests);
    }

    [TestMethod]
    public async Task With_no_api_requests_left_a_head_on_the_api_is_not_sent()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Api, HttpStatusCode.OK, "{}", ("x-ratelimit-remaining", "0"), ("x-ratelimit-reset", ResetIn40Minutes(bed)));

        _ = await bed.GetText(Api);
        NetException failure = await bed.HeadFails(Api);

        Assert.AreEqual(NetErrorKind.RateLimited, failure.Kind);
        Assert.HasCount(1, bed.Server.Requests);
    }

    private static string ResetIn40Minutes(TestBed bed) =>
        bed.Start.AddMinutes(40).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
}
