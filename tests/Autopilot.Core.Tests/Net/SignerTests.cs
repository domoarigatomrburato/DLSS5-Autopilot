using Autopilot.Core.Net;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Tests.Net;

/// <summary>A signed download passes only when Windows trusts the signature and its signer is exactly the publisher.</summary>
[TestClass]
public sealed class SignerTests(TestContext testContext)
{
    private static readonly SignerVerification _nvidia = new(FakeSigner.Nvidia);

    // Every status Windows gives a signature it does not accept.
    private static readonly SignatureStatus[] _notAccepted = [SignatureStatus.Invalid, SignatureStatus.Untrusted, SignatureStatus.Expired, SignatureStatus.Unreadable];

    public static IEnumerable<object[]> NotAccepted => _notAccepted.Select(status => new object[] { status });

    [TestMethod]
    public async Task A_file_signed_by_its_publisher_is_cached()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);
        bed.Signatures.Answer(FakeSigner.SignedBy(FakeSigner.Nvidia));

        CachedFile file = await bed.Download(_nvidia);

        Assert.AreEqual(_nvidia, file.VerifiedBy);
        Assert.AreEqual(file.Path + ".part", bed.Signatures.Checked[0], "the check runs on the .part file, before the move");
    }

    [TestMethod]
    [DataRow("Contoso Ltd")]
    [DataRow("Nvidia Corporation")]
    [DataRow("NVIDIA Corporation ")]
    public async Task A_file_signed_by_someone_else_is_refused_naming_both(string signer)
    {
        Refusal refusal = await RefusalFor(FakeSigner.SignedBy(signer));

        Assert.AreEqual(VerificationProblem.WrongSigner, refusal.Problem);
        Assert.Contains($"signed by {signer}, not by {FakeSigner.Nvidia}", refusal.Message);
        Assert.AreEqual(TextFor("NetVerificationFailedWrongSigner"), refusal.Explanation);
        Assert.AreEqual(0, refusal.Cached);
    }

    [TestMethod]
    public async Task A_file_without_a_signature_is_refused_as_not_signed()
    {
        Refusal refusal = await RefusalFor(FakeSigner.Unsigned);

        Assert.AreEqual(VerificationProblem.NotSigned, refusal.Problem);
        Assert.AreEqual(TextFor("NetVerificationFailedNotSigned"), refusal.Explanation);
        Assert.AreEqual(0, refusal.Cached);
    }

    [TestMethod]
    [DynamicData(nameof(NotAccepted))]
    public async Task A_signature_windows_does_not_accept_is_refused_even_by_the_right_signer(SignatureStatus status)
    {
        Refusal refusal = await RefusalFor(new SignatureCheck(status, FakeSigner.Nvidia, "0x800B0109: the reason Windows gave"));

        Assert.AreEqual(VerificationProblem.SignatureInvalid, refusal.Problem);
        Assert.Contains($"is {status}", refusal.Message);
        Assert.Contains("0x800B0109: the reason Windows gave", refusal.Message);
        Assert.AreEqual(TextFor("NetVerificationFailedSignatureInvalid"), refusal.Explanation);
        Assert.AreEqual(0, refusal.Cached);
        Assert.AreEqual(1, refusal.Requests, "a refused signature is never retried");
    }

    [TestMethod]
    public async Task A_cached_file_still_signed_by_its_publisher_is_used_without_a_request()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);
        _ = await bed.Download(_nvidia);

        _ = await bed.Download(_nvidia);

        Assert.HasCount(2, bed.Signatures.Checked, "the download, the cached entry");
        Assert.AreEqual(1, bed.Requests);
    }

    public static IEnumerable<object[]> ChecksThatFailACachedFile() =>
    [
        [FakeSigner.SignedBy("Contoso Ltd")],
        [FakeSigner.Unsigned],
        .. _notAccepted.Select(status => new object[] { new SignatureCheck(status, FakeSigner.Nvidia, "reason") }),
    ];

    [TestMethod]
    [DynamicData(nameof(ChecksThatFailACachedFile))]
    public async Task A_cached_file_that_no_longer_passes_is_fetched_again(SignatureCheck cached)
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);
        SignatureCheck good = FakeSigner.SignedBy(FakeSigner.Nvidia);
        bed.Signatures.Answer(good, cached, good);
        _ = await bed.Download(_nvidia);

        CachedFile again = await bed.Download(_nvidia);

        Assert.HasCount(3, bed.Signatures.Checked, "the download, the cached entry, the new download");
        Assert.AreEqual(2, bed.Requests);
        Assert.AreEqual(_nvidia, again.VerifiedBy);
    }

    [TestMethod]
    [DynamicData(nameof(ChecksThatFailACachedFile))]
    public async Task A_cached_file_that_no_longer_passes_is_not_found(SignatureCheck cached)
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);
        CachedFile downloaded = await bed.Download(_nvidia);
        bed.Signatures.Answer(cached);

        CachedFile? found = await Find(bed);

        Assert.IsNull(found);
        Assert.IsTrue(File.Exists(downloaded.Path), "a reader writes nothing");
    }

    [TestMethod]
    public async Task A_cached_file_dropped_for_its_signature_says_why_in_the_log()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);
        _ = await bed.Download(_nvidia);
        bed.Signatures.Answer(FakeSigner.SignedBy("Someone Else"));

        _ = await Find(bed);

        Assert.AreEqual($"The cached {CacheBed.Name} is no longer used: it is signed by Someone Else, not by {FakeSigner.Nvidia}", bed.Log.LatestRecord.Message);
    }

    private static string TextFor(string id) => Texts.Format(id, "example.test", "", CacheBed.Name);

    private Task<CachedFile?> Find(CacheBed bed) =>
        bed.Cache.FindAsync(new(CacheBed.Url), TestBed.Hosts, CacheBed.Name, _nvidia, testContext.CancellationToken);

    // NVIDIA's file downloaded while the signature check answers `answer`, which must refuse it.
    private async Task<Refusal> RefusalFor(SignatureCheck answer)
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);
        bed.Signatures.Answer(answer);
        NetException failure = await bed.DownloadFails(_nvidia);
        VerificationFailedDetail detail = Assert.IsInstanceOfType<VerificationFailedDetail>(failure.Detail);
        Assert.AreEqual(CacheBed.Name, detail.FileName);
        return new(detail.Problem, failure.Message, NetExplanations.Explain(failure), bed.Files.Cached.Length, bed.Requests);
    }

    private sealed record Refusal(VerificationProblem Problem, string Message, string Explanation, int Cached, int Requests);
}
