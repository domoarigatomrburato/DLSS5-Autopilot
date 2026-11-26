using System.Globalization;
using System.Net;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

/// <summary>A sums file that cannot vouch for the download refuses it; it never falls back to unverified.</summary>
[TestClass]
public sealed class SumsFileTests(TestContext testContext)
{
    private const string Other = VerificationTests.OtherDigest;
    private static readonly SumsFileVerification _sums = new(new(CacheBed.SumsUrl));

    [TestMethod]
    public async Task A_sums_file_that_lists_the_name_verifies_the_download_by_its_digest()
    {
        using CacheBed bed = new(testContext);
        _ = bed.Serve(CacheFixtures.Zip).Server.Answer(CacheBed.SumsUrl, HttpStatusCode.OK, $"{Other}  other.zip\n{CacheFixtures.ZipDigest.ToUpperInvariant()} *tool.zip\r\n");

        CachedFile file = await bed.Download(_sums);

        Assert.AreEqual(CacheFixtures.ZipVerification, file.VerifiedBy);
        Assert.AreEqual(bed.Files.ZipEntry, file.Path);
    }

    [TestMethod]
    [DataRow("\uFEFF{0}  tool.zip\n", DisplayName = "a byte order mark before the first line")]
    [DataRow("{0} tool.zip\n", DisplayName = "one space between the digest and the name")]
    public async Task A_sums_file_in_another_common_shape_still_lists_the_name(string format)
    {
        using CacheBed bed = new(testContext);
        ServeSums(bed, string.Format(CultureInfo.InvariantCulture, format, CacheFixtures.ZipDigest));

        CachedFile file = await bed.Download(_sums);

        Assert.AreEqual(CacheFixtures.ZipVerification, file.VerifiedBy);
    }

    [TestMethod]
    public async Task A_sums_file_with_another_digest_refuses_the_download()
    {
        using CacheBed bed = new(testContext);
        _ = bed.Serve(CacheFixtures.Zip).Server.Answer(CacheBed.SumsUrl, HttpStatusCode.OK, $"{Other}  tool.zip\n");

        NetException failure = await bed.DownloadFails(_sums);

        Assert.AreEqual(VerificationProblem.DigestMismatch, ProblemOf(failure));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.NotFound, "", VerificationProblem.SumsUnavailable)]
    [DataRow(HttpStatusCode.OK, Other + "  other.zip\n", VerificationProblem.NotInSums)]
    [DataRow(HttpStatusCode.OK, Other + "  tool.zip.sig\n" + Other + "  TOOL.ZIP\n", VerificationProblem.NotInSums)]
    [DataRow(HttpStatusCode.OK, Other + "  tool.zip\n" + Other + " *tool.zip\n", VerificationProblem.NotInSums)]
    public async Task A_sums_file_that_cannot_vouch_for_the_name_refuses_it(HttpStatusCode status, string body, VerificationProblem problem)
    {
        using CacheBed bed = new(testContext);
        bed.Server.Answer(CacheBed.SumsUrl, status, body);

        NetException failure = await bed.DownloadFails(_sums);

        Assert.AreEqual(problem, ProblemOf(failure));
        Assert.AreEqual(0, bed.Server.RequestsTo(CacheBed.Url), "the file itself is never asked for");
        Assert.Contains("example.test", NetExplanations.Explain(failure));
    }

    [TestMethod]
    public async Task An_unreachable_sums_file_refuses_the_download()
    {
        using CacheBed bed = new(testContext, TestBed.SingleAttempt);
        _ = bed.Server.Throw(CacheBed.SumsUrl, new HttpRequestException(HttpRequestError.NameResolutionError));

        NetException failure = await bed.DownloadFails(_sums);

        Assert.AreEqual(VerificationProblem.SumsUnavailable, ProblemOf(failure));
        Assert.Contains("(Dns)", failure.Message);
    }

    [TestMethod]
    public async Task A_sums_file_is_asked_for_once_while_its_answer_is_fresh()
    {
        using CacheBed bed = new(testContext);
        _ = bed.Serve(CacheFixtures.Zip).Server.Answer(CacheBed.SumsUrl, HttpStatusCode.OK, $"{CacheFixtures.ZipDigest}  tool.zip\n");

        _ = await bed.Download(_sums);
        CachedFile again = await bed.Download(_sums);

        Assert.AreEqual(CacheFixtures.ZipVerification, again.VerifiedBy);
        Assert.AreEqual(1, bed.Server.RequestsTo(CacheBed.SumsUrl));
    }

    [TestMethod]
    public async Task An_old_sums_answer_vouches_for_the_download_when_the_network_fails()
    {
        using CacheBed bed = new(testContext, TestBed.SingleAttempt);
        await DownloadThenLoseTheSumsFile(bed);
        bed.Bed.Advance(TimeSpan.FromHours(7).TotalSeconds);

        CachedFile again = await bed.Download(_sums);

        Assert.AreEqual(CacheFixtures.ZipVerification, again.VerifiedBy);
        Assert.AreEqual(2, bed.Server.RequestsTo(CacheBed.SumsUrl), "an old answer is asked for again first");
    }

    private static void ServeSums(CacheBed bed, string body) =>
        bed.Serve(CacheFixtures.Zip).Server.Answer(CacheBed.SumsUrl, HttpStatusCode.OK, body);

    private static async Task DownloadThenLoseTheSumsFile(CacheBed bed)
    {
        _ = bed.Serve(CacheFixtures.Zip).Server
            .Answer(CacheBed.SumsUrl, HttpStatusCode.OK, $"{CacheFixtures.ZipDigest}  tool.zip\n")
            .Throw(CacheBed.SumsUrl, new HttpRequestException(HttpRequestError.NameResolutionError));
        _ = await bed.Download(_sums);
    }

    private static VerificationProblem ProblemOf(NetException failure) =>
        Assert.IsInstanceOfType<VerificationFailedDetail>(failure.Detail).Problem;
}
