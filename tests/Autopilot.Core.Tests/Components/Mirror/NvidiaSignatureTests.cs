using Autopilot.Core.Components;
using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Net;
using Autopilot.Core.Tests.Executables;
using Autopilot.Core.Tests.Net;

namespace Autopilot.Core.Tests.Components.Mirror;

[TestClass]
public sealed class NvidiaSignatureTests(TestContext testContext)
{
    private static readonly byte[] _dll = new TestPe(TestPe.Amd64) { Size = 300_000 }.ToBytes();

    [TestMethod]
    public async Task Nvidias_raw_runtime_is_refused_without_nvidias_signature()
    {
        BuildEntry entry = (await MirrorBed.Captured(testContext)).Pick(BuildFamily.Dlss, null).Entry;

        NetException failure = await DownloadUnsigned(entry);

        Assert.AreEqual(new VerificationFailedDetail(VerificationProblem.NotSigned, "nvngx_dlss.dll"), failure.Detail);
    }

    // Through the download cache, with a signature check that says no.
    private async Task<NetException> DownloadUnsigned(BuildEntry entry)
    {
        using CacheBed cache = new(testContext, TestBed.SingleAttempt);
        cache.Signatures.Answer(false);
        _ = cache.Server.AnswerBytes(entry.Url.AbsoluteUri, _dll);
        return await Assert.ThrowsAsync<NetException>(() => Download(cache, entry));
    }

    private Task<CachedFile> Download(CacheBed cache, BuildEntry entry) =>
        cache.Cache.DownloadAsync(entry.Url, ComponentHosts.Allowlist, entry.FileName, entry.Download, null, null, testContext.CancellationToken);
}
