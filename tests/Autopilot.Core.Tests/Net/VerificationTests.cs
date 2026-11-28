using Autopilot.Core.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class VerificationTests(TestContext testContext)
{
    internal const string OtherDigest = "0000000000000000000000000000000000000000000000000000000000000000";

    [TestMethod]
    public async Task A_file_whose_digest_does_not_match_is_refused_and_never_cached()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);

        NetException failure = await bed.DownloadFails(new Sha256Verification(OtherDigest));

        Assert.AreEqual(NetErrorKind.VerificationFailed, failure.Kind);
        Assert.AreEqual(VerificationProblem.DigestMismatch, ProblemOf(failure));
        Assert.IsEmpty(bed.Files.Cached);
        Assert.AreEqual(1, bed.Requests, "a failed verification is never retried");
    }

    [TestMethod]
    public async Task A_digest_in_capitals_is_the_same_digest()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);

        CachedFile file = await bed.Download(new Sha256Verification(CacheFixtures.ZipDigest.ToUpperInvariant()));

        Assert.AreEqual(bed.Files.ZipEntry, file.Path);
    }

    [TestMethod]
    [DataRow("abc")]
    [DataRow("zz00000000000000000000000000000000000000000000000000000000000000")]
    public void A_digest_that_is_not_64_hex_digits_is_a_programming_error(string digest) =>
        Assert.ThrowsExactly<ArgumentException>(() => new Sha256Verification(digest));

    [TestMethod]
    public void An_unverified_download_needs_a_reason() =>
        Assert.ThrowsExactly<ArgumentException>(() => new NoVerification(" "));

    [TestMethod]
    public async Task A_publisher_replacing_an_asset_under_the_same_url_gets_a_new_entry()
    {
        using CacheBed bed = new(testContext);
        byte[] replaced = [.. CacheFixtures.Zip, 0];
        _ = bed.Serve(CacheFixtures.Zip, replaced);
        CachedFile old = await bed.Download();

        CachedFile current = await bed.Download(new Sha256Verification(CacheFixtures.DigestOf(replaced)));

        Assert.AreNotEqual(old.Path, current.Path);
        Assert.HasCount(2, bed.Files.Cached);
        Assert.AreEqual(replaced.Length, current.Size);
    }

    [TestMethod]
    public async Task An_unverified_download_is_kept_and_its_reason_logged()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);

        CachedFile file = await bed.Download(new NoVerification("the publisher offers no checksum"));

        Assert.AreEqual(CacheFixtures.Zip.Length, file.Size);
        Assert.AreEqual(LogLevel.Warning, bed.Log.LatestRecord.Level);
        Assert.AreEqual("tool.zip from example.test is used without verification: the publisher offers no checksum", bed.Log.LatestRecord.Message);
    }

    private static VerificationProblem ProblemOf(NetException failure) =>
        Assert.IsInstanceOfType<VerificationFailedDetail>(failure.Detail).Problem;
}
