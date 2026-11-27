using System.Text.Json.Nodes;
using Autopilot.Core.Components;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components;

/// <summary>Which release and which archive each OptiScaler build takes, from the forks' captured release pages.</summary>
[TestClass]
public sealed class OptiScalerChoiceTests(TestContext testContext)
{
    private static readonly OptiScalerBuild _wilsjo2 = OptiScalerBuild.Wilsjo2;

    [TestMethod]
    public async Task Wilsjo2s_build_takes_the_standard_zip_beside_the_mfg_one_and_another_program()
    {
        using ComponentsBed bed = new(testContext);
        bed.Fixture(OptiScalerFixtures.List(_wilsjo2), "optiscaler-wilsjo2-releases.json");

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(_wilsjo2, bed.Cancellation);

        Assert.AreEqual("v0.10.0", release.Version.Label, "the tag is the version recorded");
        Assert.AreEqual("OptiScaler-NR-v0.10.0.zip", release.Archive.Name);
        Assert.AreEqual(
            new SumsFileVerification(new(ComponentsBed.Download(_wilsjo2.Repository, "v0.10.0", "OptiScaler-NR-v0.10.0-SHA256SUMS.txt")), "OptiScaler-NR-v0.10.0.zip"),
            release.Verification,
            "the release's sums file comes before the API's digest");
        Assert.AreEqual(
            "OptiScaler v0.10.0 carries display-filter-v0.9.0-preview.zip, OptiScaler-NR-v0.10.0-rtx40-mfg.zip, OptiScaler-NR-v0.10.0-SHA256SUMS.txt, OptiScaler-NR-v0.10.0.zip: installing OptiScaler-NR-v0.10.0.zip",
            Assert.ContainsSingle(bed.OptiScalerLines));
    }

    [TestMethod]
    public async Task The_mfg_build_takes_only_the_rtx40_mfg_package()
    {
        using ComponentsBed bed = new(testContext);
        bed.Fixture(OptiScalerFixtures.List(_wilsjo2), "optiscaler-wilsjo2-releases.json");

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(OptiScalerBuild.Wilsjo2Mfg, bed.Cancellation);

        Assert.AreEqual("OptiScaler-NR-v0.10.0-rtx40-mfg.zip", release.Archive.Name);
        Assert.AreEqual("OptiScaler-DLSSNR-v0.10.0-OptiScaler-NR-v0.10.0-rtx40-mfg.zip", release.CacheName, "the two zips of one release never share an entry");
    }

    [TestMethod]
    public async Task A_newer_release_of_another_program_is_passed_over()
    {
        using ComponentsBed bed = new(testContext);
        JsonArray releases = ReleaseJsonText.Captured("optiscaler-wilsjo2-releases.json");
        OptiScalerFixtures.RemoveNewest(releases, 3);
        bed.Json(OptiScalerFixtures.List(_wilsjo2), releases);

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(_wilsjo2, bed.Cancellation);

        Assert.AreEqual("v0.9.5", release.Tag, "display-filter-v0.6.0-preview was published a minute later (#364)");
        Assert.AreEqual(
            "OptiScaler display-filter-v0.6.0-preview carries display-filter-v0.6.0-preview.zip, SHA256SUMS.txt: no OptiScaler package among them, passing it over",
            bed.OptiScalerLines[0]);
    }

    [TestMethod]
    public async Task The_mfg_build_never_takes_the_standard_zip_under_its_name()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(OptiScalerFixtures.List(_wilsjo2), ReleaseJsonText.List(OptiScalerFixtures.Release(_wilsjo2, "v0.8.2", 0, "OptiScaler-NR-v0.8.2.zip")));

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.OptiScaler.ResolveAsync(OptiScalerBuild.Wilsjo2Mfg, bed.Cancellation));

        Assert.AreEqual(ComponentFailure.NoPackage, failure.Reason);
        Assert.AreEqual(
            "No recent release on wilsjo2/OptiScaler-DLSSNR-PreSR-Multipass carries the rtx40-mfg package any more. Choose \"wilsjo2's fork\" in 'optiscaler build' for the standard one.",
            failure.Message);
        Assert.AreEqual(
            "OptiScaler v0.8.2 carries OptiScaler-NR-v0.8.2.zip: none is the rtx40-mfg package, passing it over",
            Assert.ContainsSingle(bed.OptiScalerLines));
    }

    [TestMethod]
    public async Task Y4my4my4ms_build_skips_nightly_and_the_with_dlss_archive_and_ignores_the_pre_release_flag()
    {
        using ComponentsBed bed = new(testContext);
        bed.Fixture(OptiScalerFixtures.List(OptiScalerBuild.Y4my4my4m), "optiscaler-y4my4my4m-releases.json");

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(OptiScalerBuild.Y4my4my4m, bed.Cancellation);

        Assert.AreEqual("v10.0.0-dev-fork-y4my4my4m-v4", release.Tag);
        Assert.AreEqual("OptiScaler_v10.0.0-dev-fork-y4my4my4m-v4_20260905.7z", release.Archive.Name);
        Assert.AreEqual("OptiScaler-DLSSNR-v10.0.0-dev-fork-y4my4my4m-v4-OptiScaler_v10.0.0-dev-fork-y4my4my4m-v4_20260905.7z", release.CacheName);
    }

    [TestMethod]
    public async Task Janblades_build_skips_the_test_zip()
    {
        using ComponentsBed bed = new(testContext);
        OptiScalerBuild janblade = OptiScalerBuild.Janblade;
        bed.Json(OptiScalerFixtures.List(janblade), ReleaseJsonText.List(OptiScalerFixtures.Release(janblade, "v0.1.28", 0, "OptiScaler-DLSSNR-F5-test-0.1.28-dxvk-reframework-3f2a9c1.zip", "OptiScaler-DLSSNR-F5-0.1.28.zip")));

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(janblade, bed.Cancellation);

        Assert.AreEqual("OptiScaler-DLSSNR-F5-0.1.28.zip", release.Archive.Name);
    }

    [TestMethod]
    public async Task Janblades_current_release_takes_its_package_beside_the_optional_extras()
    {
        using ComponentsBed bed = new(testContext);
        bed.Fixture(OptiScalerFixtures.List(OptiScalerBuild.Janblade), "optiscaler-janblade-releases.json");

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(OptiScalerBuild.Janblade, bed.Cancellation);

        Assert.AreEqual("v0.1.39-Optiscaler-F5.zip", release.Archive.Name);
    }

    [TestMethod]
    public async Task When_every_archive_looks_like_a_variant_all_are_kept_and_the_shortest_stem_wins()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(OptiScalerFixtures.List(_wilsjo2), ReleaseJsonText.List(OptiScalerFixtures.Release(_wilsjo2, "v1.0.0", 0, "OptiScaler-NR-v1.0.0-rtx40-mfg-debug.zip", "OptiScaler-NR-v1.0.0-rtx40-mfg.zip")));

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(_wilsjo2, bed.Cancellation);

        Assert.AreEqual("OptiScaler-NR-v1.0.0-rtx40-mfg.zip", release.Archive.Name);
        StringAssert.Contains(bed.OptiScalerLines[0], "every archive looks like a variant", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Against_an_unrelated_archive_the_releases_own_order_stands()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(OptiScalerFixtures.List(_wilsjo2), ReleaseJsonText.List(OptiScalerFixtures.Release(_wilsjo2, "v1.0.0", 0, "OptiScaler-NR-v1.0.0-full.zip", "OptiScaler-Symbols.zip")));

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(_wilsjo2, bed.Cancellation);

        Assert.AreEqual("OptiScaler-NR-v1.0.0-full.zip", release.Archive.Name, "the shorter name is not the stem of the other");
    }

    [TestMethod]
    public async Task A_page_with_no_package_in_its_recent_releases_is_refused()
    {
        using ComponentsBed bed = new(testContext);
        OptiScalerBuild fork = OptiScalerBuild.Y4my4my4m;
        bed.Json(OptiScalerFixtures.List(fork), ReleaseJsonText.List(OptiScalerFixtures.Release(fork, "nightly", 0, "OptiScaler_v10.0.0-pre1_20261126.7z")));

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.OptiScaler.ResolveAsync(fork, bed.Cancellation));

        Assert.AreEqual(
            "No recent release on y4my4my4m/OptiScaler_DLSSNR_Multipass_MFG carries an OptiScaler package (.zip or .7z). Pick another 'optiscaler build' under settings, or try again later.",
            failure.Message);
    }

    [TestMethod]
    public async Task A_draft_is_never_taken()
    {
        using ComponentsBed bed = new(testContext);
        JsonObject draft = OptiScalerFixtures.Release(_wilsjo2, "v2.0.0", 0, "OptiScaler-NR-v2.0.0.zip");
        draft["draft"] = true;
        bed.Json(OptiScalerFixtures.List(_wilsjo2), ReleaseJsonText.List(draft, OptiScalerFixtures.Release(_wilsjo2, "v1.9.0", 1, "OptiScaler-NR-v1.9.0.zip")));

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(_wilsjo2, bed.Cancellation);

        Assert.AreEqual("v1.9.0", release.Tag);
    }
}
