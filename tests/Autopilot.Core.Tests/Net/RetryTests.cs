using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class RetryTests(TestContext testContext)
{
    private const string Url = "https://example.test/data.json";

    [TestMethod]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    [DataRow(HttpStatusCode.InternalServerError)]
    [DataRow(HttpStatusCode.RequestTimeout)]
    public async Task A_server_that_hiccups_is_asked_again_after_one_then_four_seconds(HttpStatusCode hiccup)
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, hiccup).Answer(Url, hiccup).Answer(Url, HttpStatusCode.OK, "fine");

        Task<TextAnswer> pending = bed.GetText(Url);
        bed.Advance(1, 4);
        TextAnswer answer = await pending;

        Assert.AreEqual("fine", answer.Body);
        CollectionAssert.AreEqual(new[] { 0.0, 1, 5 }, bed.RequestSeconds);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_server_that_keeps_failing_is_asked_three_times_then_reported()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.BadGateway);

        Task<NetException> pending = bed.GetTextFails(Url);
        bed.Advance(1, 4);
        NetException failure = await pending;

        Assert.AreEqual(NetErrorKind.HttpStatus, failure.Kind);
        Assert.AreEqual(new HttpStatusDetail(HttpStatusCode.BadGateway), failure.Detail);
        Assert.HasCount(3, bed.Server.Requests);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_not_found_is_the_answer_at_once_with_its_body()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.NotFound, "no such release");

        TextAnswer answer = await bed.GetText(Url);

        Assert.AreEqual(HttpStatusCode.NotFound, answer.StatusCode);
        Assert.AreEqual("no such release", answer.Body);
        Assert.HasCount(1, bed.Server.Requests);
    }

    [TestMethod]
    public async Task Retry_after_in_seconds_replaces_the_wait()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.ServiceUnavailable, "", ("Retry-After", "10")).Answer(Url, HttpStatusCode.OK, "fine");

        Task<TextAnswer> pending = bed.GetText(Url);
        bed.Advance(9);
        Assert.HasCount(1, bed.Server.Requests);
        bed.Advance(1);
        await pending;

        CollectionAssert.AreEqual(new[] { 0.0, 10 }, bed.RequestSeconds);
    }

    [TestMethod]
    public async Task Retry_after_as_a_date_replaces_the_wait()
    {
        using TestBed bed = new(testContext);
        string date = bed.Start.AddSeconds(20).ToString("R", CultureInfo.InvariantCulture);
        bed.Server.Answer(Url, HttpStatusCode.ServiceUnavailable, "", ("Retry-After", date)).Answer(Url, HttpStatusCode.OK, "fine");

        Task<TextAnswer> pending = bed.GetText(Url);
        bed.Advance(20);
        await pending;

        CollectionAssert.AreEqual(new[] { 0.0, 20 }, bed.RequestSeconds);
    }

    [TestMethod]
    public async Task A_retry_after_over_a_minute_ends_the_attempts_saying_when_the_server_answers_again()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.ServiceUnavailable, "", ("Retry-After", "120"));

        NetException failure = await bed.GetTextFails(Url);

        Assert.AreEqual(NetErrorKind.RateLimited, failure.Kind);
        Assert.AreEqual(bed.Start.AddSeconds(120), RetryAtOf(failure));
        Assert.HasCount(1, bed.Server.Requests);
    }

    [TestMethod]
    public async Task A_dropped_connection_is_retried()
    {
        using TestBed bed = new(testContext);
        bed.Server.Throw(Url, Transport(HttpRequestError.ConnectionError, SocketError.ConnectionReset)).Answer(Url, HttpStatusCode.OK, "fine");

        Task<TextAnswer> pending = bed.GetText(Url);
        bed.Advance(1);
        TextAnswer answer = await pending;

        Assert.AreEqual("fine", answer.Body);
    }

    [TestMethod]
    public async Task A_body_shorter_than_its_length_is_retried_then_reported_as_cut()
    {
        using TestBed bed = new(testContext);
        bed.Server.AnswerBytes(Url, [1, 2, 3], declaredLength: 10);

        Task<NetException> pending = bed.GetTextFails(Url);
        bed.Advance(1, 4);
        NetException failure = await pending;

        Assert.AreEqual(NetErrorKind.Cut, failure.Kind);
        Assert.HasCount(3, bed.Server.Requests);
    }

    [TestMethod]
    [DataRow(SocketError.HostNotFound, NetErrorKind.Dns)]
    [DataRow(SocketError.AccessDenied, NetErrorKind.Blocked)]
    public async Task A_name_or_firewall_failure_is_not_retried(SocketError code, NetErrorKind kind)
    {
        using TestBed bed = new(testContext);
        bed.Server.Throw(Url, Transport(HttpRequestError.ConnectionError, code));

        NetException failure = await bed.GetTextFails(Url);

        Assert.AreEqual(kind, failure.Kind);
        Assert.HasCount(1, bed.Server.Requests);
    }

    [TestMethod]
    public async Task A_certificate_failure_is_not_retried()
    {
        using TestBed bed = new(testContext);
        bed.Server.Throw(Url, Certificate("The remote certificate is invalid: UntrustedRoot"));

        NetException failure = await bed.GetTextFails(Url);

        Assert.AreEqual(NetErrorKind.Certificate, failure.Kind);
        Assert.HasCount(1, bed.Server.Requests);
    }

    [TestMethod]
    public async Task A_programming_error_is_not_retried_nor_dressed_as_a_network_failure()
    {
        using TestBed bed = new(testContext);
        bed.Server.Throw(Url, new InvalidOperationException("a bug"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => bed.GetText(Url));

        Assert.HasCount(1, bed.Server.Requests);
    }

    internal static HttpRequestException Transport(HttpRequestError error, SocketError socket) =>
        new(error, "transport failure", new SocketException((int)socket));

    internal static HttpRequestException Certificate(string reason) =>
        new(HttpRequestError.SecureConnectionError, "The SSL connection could not be established", new System.Security.Authentication.AuthenticationException(reason));

    private static DateTimeOffset? RetryAtOf(NetException failure) =>
        Assert.IsInstanceOfType<RateLimitedDetail>(failure.Detail).RetryAt;
}
