using System.Net;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

/// <summary>The preview finds what the cache holds without a request; a thrown-away download is deleted under its lock.</summary>
[TestClass]
public sealed class CacheOnlyTests(TestContext testContext)
{
    private static readonly SumsFileVerification _sums = new(new(CacheBed.SumsUrl));

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_finished_download_is_found_without_a_request_even_while_its_lock_is_held()
    {
        using CacheBed bed = new(testContext);
        TestFiles.Plant(bed.Files.ZipEntry, CacheFixtures.Zip);
        using FileStream held = bed.Files.HoldLock(bed.Files.ZipEntry);

        CachedFile? found = await Find(bed, CacheFixtures.ZipVerification);

        Assert.AreEqual(new CachedFile(bed.Files.ZipEntry, CacheFixtures.Zip.Length, CacheFixtures.ZipVerification), found);
        Assert.AreEqual(0, bed.Requests);
    }

    [TestMethod]
    public async Task A_download_not_in_the_cache_is_not_found_and_nothing_is_asked()
    {
        using CacheBed bed = new(testContext);

        Assert.IsNull(await Find(bed, CacheFixtures.ZipVerification));
        Assert.IsNull(await Find(bed, new NoVerification("no publisher check")));
        Assert.AreEqual(0, bed.Requests);
        Assert.IsFalse(Directory.Exists(bed.Files.FilesFolder), "nothing is created");
    }

    [TestMethod]
    public async Task An_entry_that_no_longer_checks_out_is_not_found_and_left_in_place()
    {
        using CacheBed bed = new(testContext);
        TestFiles.Plant(bed.Files.ZipEntry, CacheFixtures.Page);

        Assert.IsNull(await Find(bed, CacheFixtures.ZipVerification));
        Assert.IsTrue(CacheFiles.Holds(bed.Files.ZipEntry, CacheFixtures.Page));
        Assert.AreEqual(0, bed.Requests);
    }

    [TestMethod]
    public async Task A_signed_entry_is_found_only_while_its_signer_checks_out()
    {
        using CacheBed bed = new(testContext);
        SignerVerification signer = new("ReShade");
        string entry = bed.Files.EntryFor("");
        TestFiles.Plant(entry, CacheFixtures.Zip);

        CachedFile? found = await Find(bed, signer);
        bed.Signatures.Answer(false);
        CachedFile? unsigned = await Find(bed, signer);

        Assert.AreEqual(entry, found?.Path);
        Assert.IsNull(unsigned);
        Assert.IsTrue(File.Exists(entry), "a reader writes nothing");
    }

    [TestMethod]
    public async Task A_sums_checked_download_is_found_from_the_kept_sums_answer_at_any_age()
    {
        using CacheBed bed = new(testContext);
        _ = bed.Serve(CacheFixtures.Zip).Server.Answer(CacheBed.SumsUrl, HttpStatusCode.OK, $"{CacheFixtures.ZipDigest}  tool.zip\n");
        CachedFile downloaded = await bed.Download(_sums);
        int asked = bed.Requests;
        bed.Bed.Advance(TimeSpan.FromDays(30).TotalSeconds);

        CachedFile? found = await Find(bed, _sums);

        Assert.AreEqual(downloaded, found);
        Assert.AreEqual(asked, bed.Requests, "no request, not even for the stale sums file");
    }

    [TestMethod]
    public async Task A_sums_checked_download_without_a_kept_sums_answer_is_not_found()
    {
        using CacheBed bed = new(testContext);
        TestFiles.Plant(bed.Files.ZipEntry, CacheFixtures.Zip);

        Assert.IsNull(await Find(bed, _sums));
        Assert.AreEqual(0, bed.Requests);
    }

    [TestMethod]
    public async Task A_kept_sums_answer_that_does_not_list_the_name_finds_nothing()
    {
        using CacheBed bed = new(testContext);
        _ = bed.Serve(CacheFixtures.Zip).Server.Answer(CacheBed.SumsUrl, HttpStatusCode.OK, $"{CacheFixtures.ZipDigest}  tool.zip\n");
        _ = await bed.Download(_sums);
        int asked = bed.Requests;

        Assert.IsNull(await Find(bed, _sums with { ListedName = "other.zip" }));
        Assert.AreEqual(asked, bed.Requests);
    }

    [TestMethod]
    public async Task A_thrown_away_download_is_deleted_once_its_lock_is_let_go()
    {
        using CacheBed bed = new(testContext);
        TestFiles.Plant(bed.Files.ZipEntry, CacheFixtures.Zip);
        CachedFile file = new(bed.Files.ZipEntry, CacheFixtures.Zip.Length, CacheFixtures.ZipVerification);
        FileStream held = bed.Files.HoldLock(bed.Files.ZipEntry);

        Task deleting = bed.Cache.DeleteAsync(file, Ct);
        Assert.IsFalse(deleting.IsCompleted, "the delete waits for the lock");
        Assert.IsTrue(File.Exists(bed.Files.ZipEntry));
        await held.DisposeAsync();
        bed.Bed.Advance(0.25);
        await deleting;

        Assert.IsEmpty(bed.Files.Cached);
        Assert.AreEqual(0, bed.Requests);
    }

    [TestMethod]
    public async Task Deleting_a_download_already_gone_is_not_an_error()
    {
        using CacheBed bed = new(testContext);

        await bed.Cache.DeleteAsync(new(bed.Files.ZipEntry, 1, CacheFixtures.ZipVerification), Ct);

        Assert.IsEmpty(bed.Files.Cached);
    }

    [TestMethod]
    [DataRow("an answer's folder")]
    [DataRow("a lock")]
    [DataRow("a part file")]
    [DataRow("a name with no key")]
    public async Task Only_a_download_of_this_cache_is_deleted(string what)
    {
        using CacheBed bed = new(testContext);
        string path = NotADownload(bed, what);
        TestFiles.Plant(path, CacheFixtures.Zip);

        _ = await Assert.ThrowsExactlyAsync<ArgumentException>(() => bed.Cache.DeleteAsync(new(path, 1, CacheFixtures.ZipVerification), Ct));

        Assert.IsTrue(File.Exists(path));
    }

    private static string NotADownload(CacheBed bed, string what)
    {
        string entry = bed.Files.ZipEntry;
        return what switch
        {
            "an answer's folder" => Path.Combine(bed.Bed.CacheRoot, "api", Path.GetFileName(entry)),
            "a lock" => entry + ".lock",
            "a part file" => entry + ".part",
            _ => Path.Combine(bed.Files.FilesFolder, CacheBed.Name),
        };
    }

    private Task<CachedFile?> Find(CacheBed bed, Verification verification) =>
        bed.Cache.FindAsync(new(CacheBed.Url), TestBed.Hosts, CacheBed.Name, verification, Ct);
}
