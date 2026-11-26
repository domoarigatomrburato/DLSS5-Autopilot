using System.Net;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class StreamTests(TestContext testContext)
{
    private static readonly byte[] _body = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    [TestMethod]
    public async Task A_download_lands_in_the_cache_and_reports_progress_ending_at_its_size()
    {
        using CacheBed bed = new(testContext);
        bed.Server.AnswerBytes(CacheBed.Url, _body);
        ProgressLog progress = new();

        CachedFile file = await bed.Fetch(progress: progress);

        Assert.AreEqual(10, file.Size);
        Assert.IsTrue(CacheFiles.Holds(file.Path, _body));
        Assert.AreEqual(new DownloadProgress(10, 10), progress.Reports[^1]);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_download_of_unknown_length_still_ends_at_its_size()
    {
        using CacheBed bed = new(testContext);
        bed.Server.AnswerUndeclared(CacheBed.Url, _body);
        ProgressLog progress = new();

        await bed.Fetch(progress: progress);

        Assert.AreEqual(new DownloadProgress(10, null), progress.Reports[0]);
        Assert.AreEqual(new DownloadProgress(10, 10), progress.Reports[^1]);
    }

    [TestMethod]
    public async Task A_cut_download_resumes_where_it_stopped_when_the_server_allows()
    {
        using CacheBed bed = new(testContext, TestBed.ImmediateRetries);
        CutThenResume(bed.Server);

        CachedFile file = await bed.Fetch();

        Assert.AreEqual(10, file.Size);
        Assert.IsTrue(CacheFiles.Holds(file.Path, _body));
    }

    [TestMethod]
    public async Task A_resumed_download_asks_only_for_the_rest()
    {
        using CacheBed bed = new(testContext, TestBed.ImmediateRetries);
        CutThenResume(bed.Server);

        _ = await bed.Fetch();

        CollectionAssert.AreEqual(new string?[] { null, "bytes=4-" }, bed.Bed.RequestRanges);
    }

    [TestMethod]
    public async Task A_cut_download_starts_over_when_the_server_sends_the_whole_file()
    {
        using CacheBed bed = new(testContext, TestBed.ImmediateRetries);
        bed.Server.AnswerBytes(CacheBed.Url, _body[..4], declaredLength: 10).AnswerBytes(CacheBed.Url, _body);

        CachedFile file = await bed.Fetch();

        Assert.IsTrue(CacheFiles.Holds(file.Path, _body), "the whole file, not the first 4 bytes and then all of it");
        Assert.AreEqual(10, file.Size);
        CollectionAssert.AreEqual(new string?[] { null, "bytes=4-" }, bed.Bed.RequestRanges, "the server was asked to resume, and sent everything");
    }

    [TestMethod]
    [DataRow(HttpStatusCode.RequestedRangeNotSatisfiable, "bytes */10")]
    [DataRow(HttpStatusCode.PartialContent, "bytes 2-9/10")]
    public async Task A_cut_download_starts_over_when_the_server_will_not_resume_from_its_offset(HttpStatusCode status, string range)
    {
        using CacheBed bed = new(testContext, TestBed.ImmediateRetries);
        CutThenRefuseToResume(bed.Server, status, range);

        CachedFile file = await bed.Fetch();

        Assert.AreEqual(10, file.Size);
        Assert.IsTrue(CacheFiles.Holds(file.Path, _body));
        Assert.IsNull(bed.Bed.RequestRanges[2], "the third attempt asks for the whole file");
    }

    [TestMethod]
    public async Task A_download_that_stays_cut_is_reported_as_cut_and_leaves_nothing_in_the_cache()
    {
        using CacheBed bed = new(testContext, TestBed.SingleAttempt);
        bed.Server.AnswerBytes(CacheBed.Url, _body[..4], declaredLength: 10);

        NetException failure = await bed.FetchFails();

        Assert.AreEqual(NetErrorKind.Cut, failure.Kind);
        Assert.AreEqual("example.test", failure.Host);
        Assert.IsEmpty(bed.Files.Cached);
    }

    [TestMethod]
    public async Task A_file_declared_larger_than_the_limit_is_refused_before_anything_is_kept()
    {
        using CacheBed bed = new(testContext);
        bed.Server.AnswerBytes(CacheBed.Url, _body, declaredLength: (1L << 30) + 1);

        NetException failure = await bed.FetchFails();

        Assert.AreEqual(NetErrorKind.TooLarge, failure.Kind);
        Assert.IsEmpty(bed.Files.Cached);
        Assert.HasCount(1, bed.Server.Requests, "a size limit is never retried");
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_file_that_grows_past_the_limit_while_streaming_is_refused_and_removed()
    {
        using CacheBed bed = new(testContext);
        bed.Server.AnswerUndeclared(CacheBed.Url, _body);

        NetException failure = await bed.FetchFails(new FileRequest { MaxBytes = 6 });

        Assert.AreEqual(NetErrorKind.TooLarge, failure.Kind);
        Assert.IsEmpty(bed.Files.Cached, "the partial file is removed");
        Assert.HasCount(1, bed.Server.Requests);
    }

    [TestMethod]
    public async Task A_text_answer_past_its_limit_is_refused()
    {
        using TestBed bed = new(testContext);
        bed.Server.AnswerUndeclared(CacheBed.Url, _body);

        NetException failure = await bed.GetTextFails(CacheBed.Url, new TextRequest { MaxBytes = 5 });

        Assert.AreEqual(NetErrorKind.TooLarge, failure.Kind);
    }

    [TestMethod]
    public async Task A_not_found_download_is_a_failure_with_its_status()
    {
        using CacheBed bed = new(testContext);
        bed.Server.Answer(CacheBed.Url, HttpStatusCode.NotFound, "missing");

        NetException failure = await bed.FetchFails();

        Assert.AreEqual(NetErrorKind.HttpStatus, failure.Kind);
        Assert.AreEqual(new HttpStatusDetail(HttpStatusCode.NotFound), failure.Detail);
        Assert.IsEmpty(bed.Files.Cached);
        Assert.HasCount(1, bed.Server.Requests);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    [TestMethod]
    public async Task A_part_answer_from_the_wrong_offset_is_refused()
    {
        using CacheBed bed = new(testContext, TestBed.SingleAttempt);
        bed.Server.AnswerBytes(CacheBed.Url, _body[2..], status: HttpStatusCode.PartialContent, headers: ("Content-Range", "bytes 2-9/10"));

        NetException failure = await bed.FetchFails();

        Assert.AreEqual(NetErrorKind.HttpStatus, failure.Kind);
        Assert.AreEqual(new HttpStatusDetail(HttpStatusCode.PartialContent), failure.Detail);
        Assert.IsEmpty(bed.Files.Cached);
    }

    [TestMethod]
    public async Task A_failed_write_is_not_a_network_failure_and_is_not_retried()
    {
        using CacheBed bed = new(testContext);
        bed.Server.AnswerBytes(CacheBed.Url, _body);

        await FetchFailsToWrite(bed);

        Assert.IsEmpty(bed.Files.Cached);
        Assert.HasCount(1, bed.Server.Requests);
        Assert.IsTrue(bed.Server.EveryBodyDisposed);
    }

    // A write error that is not a full drive reaches the caller as it is.
    private static Task<IOException> FetchFailsToWrite(CacheBed bed) =>
        Assert.ThrowsExactlyAsync<IOException>(() => bed.Fetch(progress: new FullDrive(new IOException("could not write the file"))));

    /// <summary>The first answer is cut after 4 of 10 bytes; the next resumes from there with a 206.</summary>
    private static void CutThenRefuseToResume(FakeServer server, HttpStatusCode status, string range) =>
        server
            .AnswerBytes(CacheBed.Url, _body[..4], declaredLength: 10)
            .AnswerBytes(CacheBed.Url, status == HttpStatusCode.PartialContent ? _body[2..] : [], status: status, headers: ("Content-Range", range))
            .AnswerBytes(CacheBed.Url, _body);

    private static void CutThenResume(FakeServer server) =>
        server
            .AnswerBytes(CacheBed.Url, _body[..4], declaredLength: 10)
            .AnswerBytes(CacheBed.Url, _body[4..], status: HttpStatusCode.PartialContent, headers: ("Content-Range", "bytes 4-9/10"));
}
