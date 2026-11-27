using System.Net;
using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Fetch;
using Autopilot.Core.Net;
using Autopilot.Core.Tests.Archives;
using Autopilot.Core.Tests.Net;

namespace Autopilot.Core.Tests.Components.Fetch;

/// <summary>A resolved component fetched into the cache and refused before anything is placed when it is not what it should be.</summary>
[TestClass]
public sealed class ComponentFetchTests(TestContext testContext)
{
    private const string Uplift = "ghostinthecamera/GITC-Uplift";
    private const string UpliftZip = "GITC-Uplift-2.1.0.zip";

    private static readonly string[] _upliftFiles = ["gitc-uplift.addon64", "gitc-uplift.addon32", "gitc-uplift-helper64.exe", "Uplift.fx", "UpliftMask.fx"];

    [TestMethod]
    public async Task Uplift_is_checked_against_its_sums_and_every_file_found_in_the_zip()
    {
        using ComponentsBed bed = new(testContext);
        byte[] zip = UpliftArchive();

        FetchedComponent fetched = await bed.Files.FetchAsync(UpliftRelease(bed, zip, SumsListing(zip)), null, bed.Cancellation);

        Assert.HasCount(5, fetched.Files);
        StringAssert.EndsWith(FetchFixtures.PathOf(fetched, "gitc-uplift.addon32"), "/Addons/gitc-uplift.addon32", StringComparison.Ordinal);
        StringAssert.EndsWith(FetchFixtures.PathOf(fetched, "UpliftMask.fx"), "/Shaders/UpliftMask.fx", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Uplift_sums_that_cannot_be_read_stop_the_install_before_the_zip_is_fetched()
    {
        using ComponentsBed bed = new(testContext);
        ComponentRelease release = UpliftRelease(bed, UpliftArchive(), null);

        NetException failure = await Assert.ThrowsAsync<NetException>(() => bed.Files.FetchAsync(release, null, bed.Cancellation));

        Assert.AreEqual(NetErrorKind.VerificationFailed, failure.Kind);
        Assert.AreEqual(0, bed.Server.RequestsTo(ComponentsBed.Download(Uplift, "v2.1.0", UpliftZip)));
    }

    // The captured sums file, listing this zip under the release's name.
    private static string SumsListing(byte[] zip) =>
        ReleaseFixtures.Fixture("gitc-uplift-v2.1.0-SHA256SUMS.txt")
            .Replace("f10f4a6d5f6c1b41b19971172d5b69d26cad5ad70afa1e2c2d433097672d73e9", CacheFixtures.DigestOf(zip), StringComparison.Ordinal);

    private static byte[] UpliftArchive() =>
        FetchFixtures.Archive(
            ArchiveFormat.Zip,
            FetchFixtures.Text("Addons/GITC-Uplift/Put nvngx_dlssnr.dll here.txt"),
            new("Addons/gitc-uplift-helper64.exe", FetchFixtures.Addon64),
            new("Addons/gitc-uplift.addon32", FetchFixtures.Addon32),
            new("Addons/gitc-uplift.addon64", FetchFixtures.Addon64),
            FetchFixtures.Text("Shaders/Uplift.fx"),
            FetchFixtures.Text("Shaders/UpliftMask.fx"),
            FetchFixtures.Text("README.md"));

    // The v2.1.0 release, its sums file served with the text given, or failing when null.
    private static ComponentRelease UpliftRelease(ComponentsBed bed, byte[] zip, string? sums)
    {
        string zipUrl = ComponentsBed.Download(Uplift, "v2.1.0", UpliftZip);
        string sumsUrl = ComponentsBed.Download(Uplift, "v2.1.0", "SHA256SUMS.txt");
        _ = bed.Server.AnswerBytes(zipUrl, zip);
        if (sums is null)
        {
            _ = bed.Unreachable(sumsUrl);
        }
        else
        {
            _ = bed.Server.Answer(sumsUrl, HttpStatusCode.OK, sums);
        }

        return FetchFixtures.Release(
            ComponentIds.Uplift,
            [new(UpliftZip, new(zipUrl), new SumsFileVerification(new(sumsUrl), UpliftZip))],
            [.. _upliftFiles.Select(name => new ExpectedFile(name))]);
    }
}
