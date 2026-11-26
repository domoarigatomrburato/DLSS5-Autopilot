using Autopilot.Core.Archives;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class DownloadCacheTests(TestContext testContext)
{
    [TestMethod]
    public async Task A_download_is_fetched_once_and_then_reused_without_a_request()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);
        CachedFile first = await bed.Download();
        ProgressLog progress = new();

        CachedFile again = await bed.Download(progress: progress);

        Assert.AreEqual(1, bed.Requests);
        Assert.AreEqual(first, again);
        Assert.IsTrue(CacheFiles.Holds(again.Path, CacheFixtures.Zip));
        Assert.AreEqual(new DownloadProgress(first.Size, first.Size), progress.Reports.Single(), "one report, at the size");
    }

    [TestMethod]
    public async Task A_download_lands_under_its_url_and_digest_with_its_own_name()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);

        CachedFile file = await bed.Download();

        Assert.AreEqual(bed.Files.ZipEntry, file.Path);
        Assert.AreEqual(CacheFixtures.Zip.Length, file.Size);
        Assert.AreEqual(CacheFixtures.ZipVerification, file.VerifiedBy);
    }

    [TestMethod]
    public async Task A_part_file_left_by_an_earlier_run_is_thrown_away()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);
        TestFiles.Plant(bed.Files.ZipEntry + ".part", CacheFixtures.Page);

        CachedFile file = await bed.Download();

        Assert.IsTrue(CacheFiles.Holds(file.Path, CacheFixtures.Zip));
        Assert.HasCount(1, bed.Files.Cached, "the .part is gone");
        Assert.IsNull(bed.Server.Requests[0].Range, "a leftover is never resumed from");
    }

    [TestMethod]
    public async Task A_bad_file_already_in_the_cache_is_fetched_again()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);
        TestFiles.Plant(bed.Files.ZipEntry, CacheFixtures.Page);

        CachedFile file = await bed.Download();

        Assert.AreEqual(1, bed.Requests);
        Assert.IsTrue(CacheFiles.Holds(file.Path, CacheFixtures.Zip));
    }

    [TestMethod]
    public async Task An_unsafe_file_name_is_refused_before_anything_is_asked()
    {
        using CacheBed bed = new(testContext);

        _ = await Assert.ThrowsAsync<UnsafePathException>(() => bed.Download(name: "..\\evil.dll"));

        Assert.AreEqual(0, bed.Requests);
    }

    [TestMethod]
    public async Task Cancelling_a_download_removes_its_part_file_and_frees_the_entry()
    {
        using CacheBed bed = new(testContext);
        bed.Server.StallBody(CacheBed.Url).AnswerBytes(CacheBed.Url, CacheFixtures.Zip);
        using CancellationTokenSource cancel = new();
        Task<CachedFile> pending = bed.Download(cancellation: cancel.Token);

        await cancel.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        Assert.IsEmpty(bed.Files.Cached);
        Assert.AreEqual(CacheFixtures.Zip.Length, (await bed.Download()).Size);
    }

    [TestMethod]
    public async Task A_second_holder_waits_for_the_lock_then_uses_the_entry_the_first_finished()
    {
        using CacheBed bed = new(testContext);
        string entry = bed.Files.ZipEntry;
        Task<CachedFile> pending;
        using (bed.Files.HoldLock(entry))
        {
            pending = bed.Download();
            bed.Bed.Advance(1);
            Assert.IsFalse(pending.IsCompleted, "FileShare.None must hold another open off, in this process too");
            TestFiles.Plant(entry, CacheFixtures.Zip);
        }

        bed.Bed.Advance(0.25);

        Assert.AreEqual(entry, (await pending).Path);
        Assert.AreEqual(0, bed.Requests);
        Assert.IsTrue(File.Exists(entry + ".lock"), "a lock file is never deleted");
    }
}
