using System.Net;
using System.Security.Cryptography;
using System.Text;
using Autopilot.Core.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class TextCacheTests(TestContext testContext)
{
    private const string Url = "https://api.github.com/repos/owner/repo/releases/latest";
    private static readonly TextRequest _cached = new() { Cached = true };

    [TestMethod]
    public async Task A_fresh_api_answer_is_reused_without_a_request()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.OK, /*lang=json,strict*/ """{"tag":"v1"}""");

        await bed.GetText(Url, _cached);
        bed.Advance(5 * 3600);
        TextAnswer again = await bed.GetText(Url, _cached);

        Assert.HasCount(1, bed.Server.Requests);
        Assert.AreEqual(/*lang=json,strict*/ """{"tag":"v1"}""", again.Body);
        Assert.AreEqual(bed.Start, again.FetchedAt);
        Assert.IsFalse(again.IsStale);
    }

    [TestMethod]
    public async Task An_api_answer_six_hours_old_is_fetched_again()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.OK, "old").Answer(Url, HttpStatusCode.OK, "new");

        await bed.GetText(Url, _cached);
        bed.Advance(6 * 3600);
        TextAnswer again = await bed.GetText(Url, _cached);

        Assert.HasCount(2, bed.Server.Requests);
        Assert.AreEqual("new", again.Body);
        Assert.AreEqual(bed.Time.GetUtcNow(), again.FetchedAt);
    }

    [TestMethod]
    public async Task When_the_server_fails_an_old_answer_is_used_and_marked_stale()
    {
        using TestBed bed = new(testContext, TestBed.SingleAttempt);
        bed.Server.Answer(Url, HttpStatusCode.OK, "old").Answer(Url, HttpStatusCode.ServiceUnavailable);

        await bed.GetText(Url, _cached);
        bed.Advance(3 * 24 * 3600);
        TextAnswer stale = await bed.GetText(Url, _cached);

        Assert.AreEqual("old", stale.Body);
        Assert.IsTrue(stale.IsStale);
        Assert.AreEqual(bed.Start, stale.FetchedAt, "the caller is told how old it is");
    }

    [TestMethod]
    public async Task When_rate_limited_an_old_answer_is_used()
    {
        using TestBed bed = new(testContext, TestBed.SingleAttempt);
        bed.Server.Answer(Url, HttpStatusCode.OK, "old").Answer(Url, HttpStatusCode.Forbidden);

        await bed.GetText(Url, _cached);
        bed.Advance(7 * 3600);
        TextAnswer stale = await bed.GetText(Url, _cached);

        Assert.IsTrue(stale.IsStale);
        Assert.AreEqual("old", stale.Body);
    }

    [TestMethod]
    public async Task With_no_old_answer_the_failure_reaches_the_caller()
    {
        using TestBed bed = new(testContext, TestBed.SingleAttempt);
        bed.Server.Answer(Url, HttpStatusCode.ServiceUnavailable);

        NetException failure = await bed.GetTextFails(Url, _cached);

        Assert.AreEqual(NetErrorKind.HttpStatus, failure.Kind);
    }

    [TestMethod]
    public async Task A_not_found_is_returned_never_cached_and_never_replaced_by_an_old_answer()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.OK, "old").Answer(Url, HttpStatusCode.NotFound, "gone");

        await bed.GetText(Url, _cached);
        bed.Advance(7 * 3600);
        TextAnswer gone = await bed.GetText(Url, _cached);
        TextAnswer stillGone = await bed.GetText(Url, _cached);

        Assert.AreEqual(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.AreEqual("gone", gone.Body);
        Assert.IsFalse(gone.IsStale);
        Assert.AreEqual(HttpStatusCode.NotFound, stillGone.StatusCode);
        Assert.HasCount(3, bed.Server.Requests);
    }

    [TestMethod]
    public async Task A_corrupt_cache_file_is_ignored_and_replaced()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.OK, "fresh");
        string file = CacheFile(bed);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, "{ not json", testContext.CancellationToken);

        TextAnswer answer = await bed.GetText(Url, _cached);
        TextAnswer again = await bed.GetText(Url, _cached);

        Assert.AreEqual("fresh", answer.Body);
        Assert.AreEqual("fresh", again.Body);
        Assert.HasCount(1, bed.Server.Requests);
        Assert.HasCount(1, Directory.GetFiles(Path.GetDirectoryName(file)!), "no temporary file is left behind");
    }

    [TestMethod]
    public async Task An_api_answer_is_kept_under_the_cache_api_folder_keyed_by_its_url()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.OK, "kept");

        await bed.GetText(Url, _cached);

        Assert.IsTrue(File.Exists(CacheFile(bed)));
    }

    [TestMethod]
    public async Task An_answer_not_marked_for_the_cache_is_not_kept()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.OK, "page");

        await bed.GetText(Url);

        Assert.IsFalse(Directory.Exists(Path.Combine(bed.CacheRoot, "api")));
    }

    [TestMethod]
    public async Task An_answer_that_cannot_be_moved_into_place_leaves_no_temporary_file()
    {
        using TestBed bed = new(testContext);
        bed.Server.Answer(Url, HttpStatusCode.OK, "fresh");
        _ = Directory.CreateDirectory(CacheFile(bed));

        TextAnswer answer = await bed.GetText(Url, _cached);

        Assert.AreEqual("fresh", answer.Body);
        Assert.IsEmpty(Directory.GetFiles(Path.Combine(bed.CacheRoot, "api")));
    }

    private static string CacheFile(TestBed bed)
    {
        string key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(new Uri(Url).AbsoluteUri)))[..32];
        return Path.Combine(bed.CacheRoot, "api", key + ".json");
    }

    [TestMethod]
    public async Task An_answer_the_cache_cannot_keep_is_still_returned_and_the_failure_logged()
    {
        const string WithQuery = Url + "?token=secret";
        using TestBed bed = new(testContext);
        bed.Server.Answer(WithQuery, HttpStatusCode.OK, "fresh");
        TestFiles.Plant(Path.Combine(bed.CacheRoot, "api"), "a file where the folder goes");

        TextAnswer answer = await bed.GetText(WithQuery, _cached);

        Assert.AreEqual("fresh", answer.Body);
        Assert.AreEqual(LogLevel.Warning, bed.Log.LatestRecord.Level);
        Assert.Contains("/repos/owner/repo/releases/latest", bed.Log.LatestRecord.Message);
        Assert.DoesNotContain("secret", bed.Log.LatestRecord.Message);
    }
}
