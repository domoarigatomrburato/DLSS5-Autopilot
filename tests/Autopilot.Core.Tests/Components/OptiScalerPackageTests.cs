using System.Net;
using Autopilot.Core.Components;
using Autopilot.Core.Net;
using Autopilot.Core.Tests.Net;

namespace Autopilot.Core.Tests.Components;

/// <summary>An OptiScaler package fetched into the cache, unpacked, and checked to be one before anything is written.</summary>
[TestClass]
public sealed class OptiScalerPackageTests(TestContext testContext)
{
    private const string Name = "OptiScaler-NR-v0.10.0.zip";
    private const string ArchiveUrl = "https://github.com/wilsjo2/OptiScaler-DLSSNR-PreSR-Multipass/releases/download/v0.10.0/" + Name;

    [TestMethod]
    public async Task A_package_with_optiscaler_dll_at_its_root_is_unpacked_in_the_cache()
    {
        using ComponentsBed bed = new(testContext);
        OptiScalerRelease release = Serve(bed, OptiScalerFixtures.Zip("OptiScaler.dll", "OptiScaler.ini", "D3D12_Optiscaler/D3D12Core.dll"));

        OptiScalerPackage package = await bed.OptiScaler.FetchAsync(release, null, bed.Cancellation);

        Assert.IsTrue(OptiScalerFixtures.Holds(package.Folder, "OptiScaler.dll"));
        Assert.IsTrue(OptiScalerFixtures.Holds(package.Folder, "D3D12_Optiscaler", "D3D12Core.dll"), "a package with folders of its own is not unwrapped");
        StringAssert.EndsWith(package.Archive.Path, "-OptiScaler-DLSSNR-v0.10.0-OptiScaler-NR-v0.10.0.zip", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task A_package_wrapped_in_one_folder_is_unwrapped()
    {
        using ComponentsBed bed = new(testContext);
        OptiScalerRelease release = Serve(bed, OptiScalerFixtures.Zip("OptiScaler-NR-v0.10.0/OptiScaler.dll", "OptiScaler-NR-v0.10.0/OptiScaler.ini"));

        OptiScalerPackage package = await bed.OptiScaler.FetchAsync(release, null, bed.Cancellation);

        Assert.AreEqual("OptiScaler-NR-v0.10.0", Path.GetFileName(package.Folder));
        Assert.IsTrue(OptiScalerFixtures.Holds(package.Folder, "OptiScaler.dll"));
    }

    [TestMethod]
    public async Task A_package_without_optiscaler_dll_is_refused_naming_what_it_holds()
    {
        using ComponentsBed bed = new(testContext);
        OptiScalerRelease release = Serve(
            bed,
            OptiScalerFixtures.Zip("CHANGELOG.md", "LICENSE.md", "README.md", "USAGE.md", "display_filter.exe", "runtime/onnxruntime.dll", "docs/README.md", "shaders/filter.hlsl"));

        OptiScalerException failure = await Assert.ThrowsAsync<OptiScalerException>(() => bed.OptiScaler.FetchAsync(release, null, bed.Cancellation));

        Assert.AreEqual(OptiScalerFailure.NotOptiScaler, failure.Reason);
        Assert.AreEqual(
            "The release v0.10.0 carries no OptiScaler.dll - it holds display_filter.exe, onnxruntime.dll, CHANGELOG.md, filter.hlsl, LICENSE.md, README.md, so it is not an OptiScaler build. Pick another 'optiscaler build' under settings, or try again later.",
            failure.Message);
    }

    [TestMethod]
    public async Task An_optiscaler_dll_only_in_a_subfolder_is_not_the_package()
    {
        using ComponentsBed bed = new(testContext);
        OptiScalerRelease release = Serve(bed, OptiScalerFixtures.Zip("bin/OptiScaler.dll", "README.md"));

        OptiScalerException failure = await Assert.ThrowsAsync<OptiScalerException>(() => bed.OptiScaler.FetchAsync(release, null, bed.Cancellation));

        StringAssert.Contains(failure.Message, "it holds OptiScaler.dll, README.md", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task An_empty_package_holds_nothing()
    {
        using ComponentsBed bed = new(testContext);
        OptiScalerRelease release = Serve(bed, OptiScalerFixtures.Zip("OptiScaler/"));

        OptiScalerException failure = await Assert.ThrowsAsync<OptiScalerException>(() => bed.OptiScaler.FetchAsync(release, null, bed.Cancellation));

        StringAssert.Contains(failure.Message, "it holds nothing", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task A_package_is_checked_against_the_sums_file_under_the_publishers_name()
    {
        using ComponentsBed bed = new(testContext);
        byte[] zip = OptiScalerFixtures.Zip("OptiScaler.dll");
        OptiScalerRelease release = Serve(bed, zip, SumsOf(bed, OptiScalerFixtures.SumsFor(zip, Name)));

        OptiScalerPackage package = await bed.OptiScaler.FetchAsync(release, null, bed.Cancellation);

        Assert.AreEqual(new Sha256Verification(CacheFixtures.DigestOf(zip)), package.Archive.VerifiedBy);
    }

    [TestMethod]
    public async Task Sums_that_cannot_be_read_stop_the_install()
    {
        using ComponentsBed bed = new(testContext);
        OptiScalerRelease release = Serve(bed, OptiScalerFixtures.Zip("OptiScaler.dll"), SumsOf(bed, null));

        NetException failure = await Assert.ThrowsAsync<NetException>(() => bed.OptiScaler.FetchAsync(release, null, bed.Cancellation));

        Assert.AreEqual(NetErrorKind.VerificationFailed, failure.Kind);
        Assert.AreEqual(0, bed.Server.RequestsTo(ArchiveUrl), "nothing is downloaded unchecked");
    }

    private static OptiScalerRelease Serve(ComponentsBed bed, byte[] zip, Verification? verification = null)
    {
        OptiScalerRelease release = OptiScalerFixtures.Resolved(Name, verification ?? new Sha256Verification(CacheFixtures.DigestOf(zip)));
        bed.Server.AnswerBytes(ArchiveUrl, zip);
        return release;
    }

    // The release's sums file: its text, or a connection that fails when null.
    private static SumsFileVerification SumsOf(ComponentsBed bed, string? text)
    {
        string url = ComponentsBed.Download(OptiScalerBuild.Wilsjo2.Repository, "v0.10.0", "OptiScaler-NR-v0.10.0-SHA256SUMS.txt");
        if (text is null)
        {
            _ = bed.Unreachable(url);
        }
        else
        {
            _ = bed.Server.Answer(url, HttpStatusCode.OK, text);
        }

        return new(new(url), Name);
    }
}
