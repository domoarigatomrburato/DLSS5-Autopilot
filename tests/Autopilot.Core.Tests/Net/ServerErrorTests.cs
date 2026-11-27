using System.Net;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

/// <summary>A 5xx whose body still holds what the caller needs, as reshade.me's 500 page with its download link.</summary>
[TestClass]
public sealed class ServerErrorTests(TestContext testContext)
{
    private const string Url = "https://example.test/data.json";
    private static readonly TextRequest _cached = new() { Cached = true };

    [TestMethod]
    public async Task A_server_error_whose_body_the_caller_accepts_is_the_answer_at_once()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.InternalServerError, "<a href=\"/downloads/setup.exe\">");
        TextRequest options = new() { AcceptServerError = body => body.Contains("/downloads/", StringComparison.Ordinal) };

        TextAnswer answer = await bed.GetText(Url, options);

        Assert.AreEqual(HttpStatusCode.InternalServerError, answer.StatusCode);
        Assert.AreEqual("<a href=\"/downloads/setup.exe\">", answer.Body);
        Assert.IsFalse(answer.IsStale);
        Assert.HasCount(1, bed.Server.Requests);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task An_accepted_server_error_is_never_kept_as_an_api_answer()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.InternalServerError, "link").Answer(Url, HttpStatusCode.OK, "fresh");
        TextRequest options = new() { Cached = true, AcceptServerError = _ => true };

        TextAnswer accepted = await bed.GetText(Url, options);
        TextAnswer again = await bed.GetText(Url, _cached);

        Assert.AreEqual(HttpStatusCode.InternalServerError, accepted.StatusCode);
        Assert.AreEqual("fresh", again.Body);
        Assert.HasCount(2, bed.Server.Requests);
    }

    [TestMethod]
    public async Task A_server_error_whose_body_the_caller_refuses_is_retried_as_any_other()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.ServiceUnavailable, "busy").Answer(Url, HttpStatusCode.ServiceUnavailable, "busy").Answer(Url, HttpStatusCode.OK, "fine");
        List<string> seen = [];
        TextRequest options = new()
        {
            AcceptServerError = body =>
            {
                seen.Add(body);
                return false;
            },
        };

        Task<TextAnswer> pending = bed.GetText(Url, options);
        bed.Advance(1, 4);
        TextAnswer answer = await pending;

        Assert.AreEqual("fine", answer.Body);
        CollectionAssert.AreEqual(new[] { 0.0, 1, 5 }, bed.RequestSeconds);
        Assert.AreEqual("busy,busy", string.Join(",", seen));
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_request_timeout_is_not_a_server_error_the_caller_can_accept()
    {
        using TestBed bed = new(testContext, TestBed.SingleAttempt);
        bed.Server.Answer(Url, HttpStatusCode.RequestTimeout, "link");
        bool asked = false;
        TextRequest options = new()
        {
            AcceptServerError = _ =>
            {
                asked = true;
                return true;
            },
        };

        NetException failure = await bed.GetTextFails(Url, options);

        Assert.AreEqual(new HttpStatusDetail(HttpStatusCode.RequestTimeout), failure.Detail);
        Assert.IsFalse(asked);
    }
}
