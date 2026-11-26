using System.Net;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class TimeoutTests(TestContext testContext)
{
    private const string Url = "https://example.test/data.json";

    [TestMethod]
    public async Task A_body_that_stops_for_thirty_seconds_is_dropped_and_asked_again()
    {
        using TestBed bed = new(testContext);
        bed.Server.StallBody(Url).Answer(Url, HttpStatusCode.OK, "fine");

        Task<TextAnswer> pending = bed.GetText(Url);
        bed.Advance(29);
        Assert.HasCount(1, bed.Server.Requests);
        bed.Advance(1, 1);
        TextAnswer answer = await pending;

        Assert.AreEqual("fine", answer.Body);
        CollectionAssert.AreEqual(new[] { 0.0, 31 }, bed.RequestSeconds);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_slow_body_that_keeps_sending_is_not_dropped()
    {
        using TestBed bed = new(testContext);
        bed.Server.Trickle(Url, bytes: 2, TimeSpan.FromSeconds(20));

        Task<TextAnswer> pending = bed.GetText(Url);
        bed.Advance(20, 20);
        TextAnswer answer = await pending;

        Assert.AreEqual(2, answer.Body.Length);
        Assert.HasCount(1, bed.Server.Requests);
    }

    [TestMethod]
    public async Task A_server_that_never_answers_is_reported_as_a_timeout()
    {
        using TestBed bed = new(testContext, TestBed.SingleAttempt);
        bed.Server.StallHeaders(Url);

        Task<NetException> pending = bed.GetTextFails(Url);
        bed.Advance(30);
        NetException failure = await pending;

        Assert.AreEqual(NetErrorKind.Timeout, failure.Kind);
        Assert.AreEqual("example.test", failure.Host);
    }

    [TestMethod]
    public async Task The_overall_limit_ends_the_request_and_is_not_retried()
    {
        using TestBed bed = new(testContext);
        bed.Server.StallBody(Url);

        Task<NetException> pending = bed.GetTextFails(Url, new TextRequest { Timeout = TimeSpan.FromSeconds(15) });
        bed.Advance(15);
        NetException failure = await pending;

        Assert.AreEqual(NetErrorKind.Timeout, failure.Kind);
        Assert.HasCount(1, bed.Server.Requests);
    }

    [TestMethod]
    public async Task Cancelling_stops_at_once_and_is_not_a_network_failure()
    {
        using TestBed bed = new(testContext);
        bed.Server.StallBody(Url);
        using CancellationTokenSource cancel = new();

        Task<TextAnswer> pending = bed.GetText(Url, cancel.Token);
        await cancel.CancelAsync();
        OperationCanceledException cancelled = await Assert.ThrowsAsync<OperationCanceledException>(() => pending);

        Assert.AreEqual(cancel.Token, cancelled.CancellationToken);
        Assert.HasCount(1, bed.Server.Requests);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task Cancelling_during_the_wait_between_attempts_stops_without_asking_again()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.ServiceUnavailable);
        using CancellationTokenSource cancel = new();

        Task<TextAnswer> pending = bed.GetText(Url, cancel.Token);
        await cancel.CancelAsync();
        bed.Advance(5);

        OperationCanceledException cancelled = await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        Assert.AreEqual(cancel.Token, cancelled.CancellationToken);
        Assert.HasCount(1, bed.Server.Requests);
    }

    [TestMethod]
    [DataRow(120, null)]
    [DataRow(null, (32L << 20) + 1)]
    public async Task A_caller_may_lower_a_text_limit_but_never_raise_one(int? seconds, long? maxBytes)
    {
        using TestBed bed = new(testContext);
        TextRequest request = new() { Timeout = seconds is { } s ? TimeSpan.FromSeconds(s) : null, MaxBytes = maxBytes };

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => bed.GetText(Url, request));

        Assert.IsEmpty(bed.Server.Requests);
    }

    [TestMethod]
    public async Task A_download_timeout_may_not_be_raised()
    {
        using CacheBed bed = new(testContext);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => bed.Fetch(new FileRequest { Timeout = TimeSpan.FromHours(3) }));

        Assert.IsEmpty(bed.Server.Requests);
        Assert.IsEmpty(bed.Files.Cached);
    }

    [TestMethod]
    public async Task A_download_limit_may_not_be_raised_past_two_gibibytes()
    {
        using CacheBed bed = new(testContext);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => bed.Fetch(new FileRequest { MaxBytes = (2L << 30) + 1 }));

        Assert.IsEmpty(bed.Server.Requests);
        Assert.IsEmpty(bed.Files.Cached);
    }

    [TestMethod]
    public async Task A_download_may_be_allowed_up_to_two_gibibytes()
    {
        using CacheBed bed = new(testContext);
        bed.Server.AnswerBytes(CacheBed.Url, [1, 2, 3]);

        CachedFile file = await bed.Fetch(new FileRequest { MaxBytes = 2L << 30 });

        Assert.AreEqual(3, file.Size);
    }
}
