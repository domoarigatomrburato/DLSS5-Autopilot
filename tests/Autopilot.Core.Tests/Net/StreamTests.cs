using System.Net;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class StreamTests(TestContext testContext)
{
    private const string Url = "https://example.test/file.zip";
    private static readonly byte[] _body = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    [TestMethod]
    public async Task A_download_lands_in_the_destination_and_reports_progress_ending_at_its_size()
    {
        using TestBed bed = new(testContext);
        bed.Server.AnswerBytes(Url, _body);
        PartFile part = new();
        ProgressLog progress = new();

        StreamResult result = await bed.Stream(Url, part, progress: progress);

        Assert.AreEqual(10, result.Size);
        CollectionAssert.AreEqual(_body, part.Content);
        Assert.AreEqual(new DownloadProgress(10, 10), progress.Reports[^1]);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_download_of_unknown_length_still_ends_at_its_size()
    {
        using TestBed bed = new(testContext);
        bed.Server.AnswerUndeclared(Url, _body);
        ProgressLog progress = new();

        await bed.Stream(Url, progress: progress);

        Assert.AreEqual(new DownloadProgress(10, null), progress.Reports[0]);
        Assert.AreEqual(new DownloadProgress(10, 10), progress.Reports[^1]);
    }

    [TestMethod]
    public async Task A_cut_download_resumes_where_it_stopped_when_the_server_allows()
    {
        using TestBed bed = new(testContext);
        CutThenResume(bed.Server);
        PartFile part = new();

        Task<StreamResult> pending = bed.Stream(Url, part);
        bed.Advance(1);
        StreamResult result = await pending;

        Assert.AreEqual(10, result.Size);
        CollectionAssert.AreEqual(_body, part.Content);
        CollectionAssert.AreEqual(new long[] { 0, 4 }, part.Opens);
    }

    [TestMethod]
    public async Task A_resumed_download_asks_only_for_the_rest()
    {
        using TestBed bed = new(testContext);
        CutThenResume(bed.Server);

        Task<StreamResult> pending = bed.Stream(Url);
        bed.Advance(1);
        await pending;

        Assert.IsNull(bed.Server.Requests[0].Range);
        Assert.AreEqual("bytes=4-", bed.Server.Requests[1].Range?.ToString());
    }

    [TestMethod]
    public async Task A_cut_download_starts_over_when_the_server_sends_the_whole_file()
    {
        using TestBed bed = new(testContext);
        bed.Server.AnswerBytes(Url, _body[..4], declaredLength: 10).AnswerBytes(Url, _body);
        PartFile part = new();

        Task<StreamResult> pending = bed.Stream(Url, part);
        bed.Advance(1);
        await pending;

        CollectionAssert.AreEqual(_body, part.Content);
        CollectionAssert.AreEqual(new long[] { 0, 0 }, part.Opens);
    }

    [TestMethod]
    public async Task A_download_that_stays_cut_is_reported_as_cut()
    {
        using TestBed bed = new(testContext, TestBed.SingleAttempt);
        bed.Server.AnswerBytes(Url, _body[..4], declaredLength: 10);

        NetException failure = await bed.StreamFails(Url);

        Assert.AreEqual(NetErrorKind.Cut, failure.Kind);
        Assert.AreEqual("example.test", failure.Host);
    }

    [TestMethod]
    public async Task A_file_declared_larger_than_the_limit_is_refused_before_anything_is_written()
    {
        using TestBed bed = new(testContext);
        bed.Server.AnswerBytes(Url, _body, declaredLength: (1L << 30) + 1);
        PartFile part = new();

        NetException failure = await bed.StreamFails(Url, part);

        Assert.AreEqual(NetErrorKind.TooLarge, failure.Kind);
        Assert.IsEmpty(part.Opens);
        Assert.HasCount(1, bed.Server.Requests, "a size limit is never retried");
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_file_that_grows_past_the_limit_while_streaming_is_refused()
    {
        using TestBed bed = new(testContext);
        bed.Server.AnswerUndeclared(Url, _body);
        PartFile part = new();

        NetException failure = await bed.StreamFails(Url, part, new FileRequest { MaxBytes = 6 });

        Assert.AreEqual(NetErrorKind.TooLarge, failure.Kind);
        Assert.IsEmpty(part.Content, "nothing past the limit is written");
        Assert.HasCount(1, bed.Server.Requests);
    }

    [TestMethod]
    public async Task A_text_answer_past_its_limit_is_refused()
    {
        using TestBed bed = new(testContext);
        bed.Server.AnswerUndeclared(Url, _body);

        NetException failure = await bed.GetTextFails(Url, new TextRequest { MaxBytes = 5 });

        Assert.AreEqual(NetErrorKind.TooLarge, failure.Kind);
    }

    [TestMethod]
    public async Task A_not_found_download_is_a_failure_with_its_status()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.NotFound, "missing");
        PartFile part = new();

        NetException failure = await bed.StreamFails(Url, part);

        Assert.AreEqual(NetErrorKind.HttpStatus, failure.Kind);
        Assert.AreEqual(HttpStatusCode.NotFound, failure.StatusCode);
        Assert.IsEmpty(part.Opens);
        Assert.HasCount(1, bed.Server.Requests);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_part_answer_from_the_wrong_offset_is_refused()
    {
        using TestBed bed = new(testContext, TestBed.SingleAttempt);
        bed.Server.AnswerBytes(Url, _body[2..], status: HttpStatusCode.PartialContent, headers: ("Content-Range", "bytes 2-9/10"));

        NetException failure = await bed.StreamFails(Url);

        Assert.AreEqual(NetErrorKind.HttpStatus, failure.Kind);
        Assert.AreEqual(HttpStatusCode.PartialContent, failure.StatusCode);
    }

    [TestMethod]
    public async Task A_failed_write_to_the_destination_is_not_a_network_failure()
    {
        using TestBed bed = new(testContext);
        bed.Server.AnswerBytes(Url, _body);

        await Assert.ThrowsExactlyAsync<IOException>(() => bed.Stream(Url, new PartFile(diskFull: true)));

        Assert.HasCount(1, bed.Server.Requests);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    /// <summary>The first answer is cut after 4 of 10 bytes; the next resumes from there with a 206.</summary>
    private static void CutThenResume(FakeServer server) =>
        server
            .AnswerBytes(Url, _body[..4], declaredLength: 10)
            .AnswerBytes(Url, _body[4..], status: HttpStatusCode.PartialContent, headers: ("Content-Range", "bytes 4-9/10"));
}
