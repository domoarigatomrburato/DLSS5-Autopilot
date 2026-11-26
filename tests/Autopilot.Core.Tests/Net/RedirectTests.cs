using System.Net;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class RedirectTests(TestContext testContext)
{
    [TestMethod]
    public async Task A_redirect_to_another_allowed_https_host_is_followed()
    {
        using TestBed bed = new(testContext);
        bed.Server
            .Redirect("https://example.test/latest", "https://cdn.example.test/file.json")
            .Answer("https://cdn.example.test/file.json", HttpStatusCode.OK, "{}");

        TextAnswer answer = await bed.GetText("https://example.test/latest");

        Assert.AreEqual(HttpStatusCode.OK, answer.StatusCode);
        Assert.AreEqual("{}", answer.Body);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_relative_redirect_is_followed_on_the_same_host()
    {
        using TestBed bed = new(testContext);
        bed.Server
            .Redirect("https://example.test/a/b", "../c", HttpStatusCode.MovedPermanently)
            .Answer("https://example.test/c", HttpStatusCode.OK, "here");

        TextAnswer answer = await bed.GetText("https://example.test/a/b");

        Assert.AreEqual("here", answer.Body);
    }

    [TestMethod]
    public async Task A_redirect_from_https_to_http_is_refused_before_the_http_server_is_asked()
    {
        using TestBed bed = new(testContext);
        bed.Server
            .Redirect("https://example.test/file", "http://example.test/file")
            .Answer("http://example.test/file", HttpStatusCode.OK, "downgraded");

        NetException failure = await bed.GetTextFails("https://example.test/file");

        Assert.AreEqual(NetErrorKind.RedirectRefused, failure.Kind);
        Assert.AreEqual(new RedirectRefusedDetail(RedirectProblem.Scheme, 1), failure.Detail);
        Assert.AreEqual(0, bed.Server.RequestsTo("http://example.test/file"));
        Assert.HasCount(1, bed.Server.Requests, "a refused redirect is never retried");
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_redirect_to_a_host_off_the_list_is_refused_before_that_host_is_asked()
    {
        using TestBed bed = new(testContext);
        bed.Server
            .Redirect("https://example.test/file", "https://evil.test/file")
            .Answer("https://evil.test/file", HttpStatusCode.OK, "elsewhere");

        NetException failure = await bed.GetTextFails("https://example.test/file");

        Assert.AreEqual(NetErrorKind.RedirectRefused, failure.Kind);
        Assert.AreEqual(RedirectProblem.Host, ProblemOf(failure));
        Assert.AreEqual("evil.test", failure.Host);
        Assert.AreEqual(0, bed.Server.RequestsTo("https://evil.test/file"));
    }

    [TestMethod]
    public async Task Five_redirects_are_followed()
    {
        using TestBed bed = new(testContext);
        for (int hop = 0; hop < 5; hop++)
        {
            bed.Server.Redirect($"https://example.test/{hop}", $"https://example.test/{hop + 1}");
        }

        bed.Server.Answer("https://example.test/5", HttpStatusCode.OK, "end");

        TextAnswer answer = await bed.GetText("https://example.test/0");

        Assert.AreEqual("end", answer.Body);
    }

    [TestMethod]
    public async Task A_redirect_loop_is_refused_after_five_hops()
    {
        using TestBed bed = new(testContext);
        bed.Server
            .Redirect("https://example.test/ping", "https://example.test/pong")
            .Redirect("https://example.test/pong", "https://example.test/ping");

        NetException failure = await bed.GetTextFails("https://example.test/ping");

        Assert.AreEqual(NetErrorKind.RedirectRefused, failure.Kind);
        Assert.AreEqual(RedirectProblem.TooManyHops, ProblemOf(failure));
        Assert.HasCount(6, bed.Server.Requests);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    [DataRow("http://example.test/file")]
    [DataRow("ftp://example.test/file")]
    [DataRow("file:///C:/Windows/win.ini")]
    public async Task An_address_that_is_not_https_is_refused_without_a_request(string address)
    {
        using TestBed bed = new(testContext);

        NetException failure = await bed.GetTextFails(address);

        Assert.AreEqual(NetErrorKind.SchemeRefused, failure.Kind);
        Assert.IsEmpty(bed.Server.Requests);
    }

    [TestMethod]
    public async Task An_address_on_a_host_off_the_list_is_refused_without_a_request()
    {
        using CacheBed bed = new(testContext);

        NetException failure = await bed.FetchFails(url: "https://evil.test/file.zip");

        Assert.AreEqual(NetErrorKind.HostNotAllowed, failure.Kind);
        Assert.AreEqual("evil.test", failure.Host);
        Assert.IsEmpty(bed.Server.Requests);
        Assert.IsEmpty(bed.Files.Cached);
    }

    [TestMethod]
    public async Task Hosts_on_the_list_match_ignoring_case_but_never_by_suffix()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer("https://example.test/a", HttpStatusCode.OK, "ok");
        HostAllowlist hosts = new("EXAMPLE.test");

        TextAnswer answer = await bed.GetText("https://Example.Test/a", hosts: hosts);
        NetException failure = await bed.GetTextFails("https://sub.example.test/a", hosts: hosts);

        Assert.AreEqual("ok", answer.Body);
        Assert.AreEqual(NetErrorKind.HostNotAllowed, failure.Kind);
    }

    [TestMethod]
    public void An_allowlist_refuses_a_wildcard() =>
        Assert.ThrowsExactly<ArgumentException>(() => new HostAllowlist("*.example.test"));

    [TestMethod]
    public async Task Every_request_names_the_tool_and_its_version()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer("https://example.test/a", HttpStatusCode.OK, "ok");

        await bed.GetText("https://example.test/a");

        Assert.AreEqual("dlss5-autopilot/1.2.3", bed.Server.Requests[0].UserAgent);
    }

    private static RedirectProblem ProblemOf(NetException failure) =>
        Assert.IsInstanceOfType<RedirectRefusedDetail>(failure.Detail).Problem;
}
