using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Net;
using Autopilot.Core.Tests.Executables;
using Autopilot.Core.Tests.Net;

namespace Autopilot.Core.Tests.Components.Mirror;

/// <summary>NVIDIA's signature and the digests pinned for community builds, run by the download cache's verifier.</summary>
[TestClass]
public sealed class NvidiaSignatureTests(TestContext testContext)
{
    private static readonly byte[] _dll = new TestPe(TestPe.Amd64) { Size = 300_000 }.ToBytes();

    [TestMethod]
    public async Task Nvidias_raw_runtime_is_refused_without_nvidias_signature()
    {
        BuildEntry entry = (await MirrorBed.Captured(testContext)).Pick(BuildFamily.Dlss, null).Entry;

        NetException failure = await Assert.ThrowsAsync<NetException>(() => Download(entry.Download, entry.FileName, signed: false));

        Assert.AreEqual(new VerificationFailedDetail(VerificationProblem.NotSigned, "nvngx_dlss.dll"), failure.Detail);
    }

    [TestMethod]
    public async Task Nvidias_own_dlssnr_build_is_refused_without_nvidias_signature()
    {
        BuildEntry entry = (await MirrorBed.Captured(testContext)).Pick(BuildFamily.Dlssnr, "310.8.0").Entry;

        NetException failure = await Assert.ThrowsAsync<NetException>(() => Download(entry.Contents!, "nvngx_dlssnr.dll", signed: false));

        Assert.AreEqual(new VerificationFailedDetail(VerificationProblem.NotSigned, "nvngx_dlssnr.dll"), failure.Detail);
    }

    [TestMethod]
    public async Task A_community_dlssnr_whose_hash_is_not_the_pin_is_refused_and_the_pinned_bytes_pass()
    {
        BuildEntry entry = (await MirrorBed.Captured(testContext)).Pick(BuildFamily.Dlssnr, "310.8.SF-v2").Entry;

        NetException failure = await Assert.ThrowsAsync<NetException>(() => Download(entry.Contents!, "nvngx_dlssnr.dll"));
        CachedFile passed = await Download(PinForTheseBytes().Verification, "nvngx_dlssnr.dll");

        Assert.AreEqual(new VerificationFailedDetail(VerificationProblem.DigestMismatch, "nvngx_dlssnr.dll"), failure.Detail);
        Assert.AreEqual(_dll.Length, passed.Size);
    }

    // A pin as the table writes one, for the test's own bytes.
    private static DlssnrPin PinForTheseBytes() => new("310.8.SF-v2", CacheFixtures.DigestOf(_dll), [75, 86, 89, 120]);

    // The DLL through the download cache, under the check given, with a signature check that answers signed.
    private async Task<CachedFile> Download(Verification check, string name, bool signed = true)
    {
        using CacheBed cache = new(testContext, TestBed.SingleAttempt);
        cache.Signatures.Answer(signed);
        _ = cache.Server.AnswerBytes(CacheBed.Url, _dll);
        return await cache.Cache.DownloadAsync(new(CacheBed.Url), TestBed.Hosts, name, check, null, null, testContext.CancellationToken);
    }
}
