using System.Net;
using Autopilot.Core.Components;
using Autopilot.Core.Gpu;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components;

/// <summary>The default build's <c>/latest</c>, the checks each release offers, and the six builds as data.</summary>
[TestClass]
public sealed class OptiScalerReleaseTests(TestContext testContext)
{
    private static readonly OptiScalerBuild _default = OptiScalerBuild.Default;

    [TestMethod]
    public async Task The_default_build_is_dagherbous_latest_release()
    {
        using ComponentsBed bed = new(testContext);
        bed.Fixture(OptiScalerFixtures.Latest(_default), "optiscaler-dagherbou-latest.json");

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(_default, bed.Cancellation);

        Assert.AreEqual("v0.2.0-dlssnr", release.Tag);
        Assert.AreEqual("OptiScaler-DLSSNR-v0.2.0.zip", release.Archive.Name);
        Assert.AreEqual(0, bed.Server.RequestsTo(OptiScalerFixtures.List(_default)));
    }

    [TestMethod]
    public async Task A_latest_release_without_a_package_falls_back_to_the_list_newest_first()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(OptiScalerFixtures.Latest(_default), OptiScalerFixtures.DagherbouTools())
            .Json(OptiScalerFixtures.List(_default), OptiScalerFixtures.DagherbouAfterTools());

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(_default, bed.Cancellation);

        Assert.AreEqual("v0.2.0-patch1", release.Tag, "published a day after v0.2.0-dlssnr; its pre-release flag is ignored");
        Assert.AreEqual("OptiScaler-DLSSNR-v0.2.0-onimusha-fix.zip", release.Archive.Name);
        Assert.AreEqual(1, OptiScalerFixtures.LinesAbout(bed, "v0.3.0-tools"), "the latest release is read once");
    }

    [TestMethod]
    public async Task Jlrouzies_build_is_checked_against_its_sums_file_by_the_assets_own_name()
    {
        using ComponentsBed bed = new(testContext);
        bed.Fixture(OptiScalerFixtures.List(OptiScalerBuild.Jlrouzies), "optiscaler-jlrouzies-releases.json");

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(OptiScalerBuild.Jlrouzies, bed.Cancellation);

        Assert.AreEqual(
            new SumsFileVerification(
                new(ComponentsBed.Download(OptiScalerBuild.Jlrouzies.Repository, "v0.8.93", "OptiScaler-NR-v0.8.93-SHA256SUMS.txt")),
                "OptiScaler-NR-v0.8.93.zip"),
            release.Verification);
    }

    [TestMethod]
    public async Task Jlrouzies_release_without_its_sums_is_refused()
    {
        using ComponentsBed bed = new(testContext);
        OptiScalerBuild jlrouzies = OptiScalerBuild.Jlrouzies;
        bed.Json(OptiScalerFixtures.List(jlrouzies), ReleaseJsonText.List(OptiScalerFixtures.Release(jlrouzies, "v0.8.94", 0, "OptiScaler-NR-v0.8.94.zip")));

        OptiScalerException failure = await Assert.ThrowsAsync<OptiScalerException>(() => bed.OptiScaler.ResolveAsync(jlrouzies, bed.Cancellation));

        Assert.AreEqual(OptiScalerFailure.SumsMissing, failure.Reason);
        Assert.AreEqual(
            "The release v0.8.94 of jlrouzies-fr/OptiScaler-DLSSNR-PreSR-Multipass carries no SHA256SUMS file, which this build publishes with every release, so its package cannot be checked and is not installed. Try again later, or pick another 'optiscaler build' under settings.",
            failure.Message);
    }

    [TestMethod]
    public async Task Without_a_digest_a_sums_file_in_the_release_is_the_check()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(OptiScalerFixtures.Latest(_default), OptiScalerFixtures.Release(_default, "v0.3.0", 0, "OptiScaler-DLSSNR-v0.3.0.zip", "SHA256SUMS.txt"));

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(_default, bed.Cancellation);

        Assert.AreEqual(new SumsFileVerification(new(ComponentsBed.Download(_default.Repository, "v0.3.0", "SHA256SUMS.txt")), "OptiScaler-DLSSNR-v0.3.0.zip"), release.Verification);
    }

    [TestMethod]
    public async Task Without_a_digest_or_sums_only_the_magic_bytes_are_checked()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(OptiScalerFixtures.Latest(_default), OptiScalerFixtures.Release(_default, "v0.3.0", 0, "OptiScaler-DLSSNR-v0.3.0.zip"));

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(_default, bed.Cancellation);

        Assert.AreEqual(new NoVerification("the release gives no digest and no sums file"), release.Verification);
    }

    [TestMethod]
    public async Task Each_build_is_resolved_once_per_run()
    {
        using ComponentsBed bed = new(testContext);
        bed.Fixture(OptiScalerFixtures.List(OptiScalerBuild.Wilsjo2), "optiscaler-wilsjo2-releases.json");

        OptiScalerRelease standard = await bed.OptiScaler.ResolveAsync(OptiScalerBuild.Wilsjo2, bed.Cancellation);
        OptiScalerRelease again = await bed.OptiScaler.ResolveAsync(OptiScalerBuild.Wilsjo2, bed.Cancellation);
        OptiScalerRelease mfg = await bed.OptiScaler.ResolveAsync(OptiScalerBuild.Wilsjo2Mfg, bed.Cancellation);

        Assert.AreSame(standard, again);
        Assert.AreNotEqual(standard.Archive, mfg.Archive);
    }

    [TestMethod]
    public async Task A_default_page_with_no_latest_release_is_read_as_a_list()
    {
        using ComponentsBed bed = new(testContext);
        bed.Server.Answer(OptiScalerFixtures.Latest(_default), HttpStatusCode.NotFound, "{}");
        bed.Fixture(OptiScalerFixtures.List(_default), "optiscaler-dagherbou-releases.json");

        OptiScalerRelease release = await bed.OptiScaler.ResolveAsync(_default, bed.Cancellation);

        Assert.AreEqual("v0.2.0-patch1", release.Tag);
    }

    [TestMethod]
    [DataRow(120, true)]
    [DataRow(86, true)]
    [DataRow(89, false)]
    public void The_mfg_package_is_refused_on_any_card_but_rtx_40(int sm, bool refused)
    {
        string? refusal = OptiScalerBuild.Wilsjo2Mfg.CardRefusal(new("NVIDIA GeForce", sm, HasTensorCores: true));

        Assert.AreEqual(refused, refusal is not null);
        Assert.IsNull(OptiScalerBuild.Wilsjo2.CardRefusal(new("NVIDIA GeForce", sm, HasTensorCores: true)), "the standard build fits any card");
    }

    [TestMethod]
    public void A_card_that_could_not_be_read_is_not_refused()
    {
        Assert.IsNull(OptiScalerBuild.Wilsjo2Mfg.CardRefusal(null));
        Assert.IsNull(OptiScalerBuild.Wilsjo2Mfg.CardRefusal(new GpuCard("Some card", null, null)));
        StringAssert.StartsWith(
            OptiScalerBuild.Wilsjo2Mfg.CardRefusal(new("NVIDIA GeForce RTX 5090", 120, true)),
            "The RTX 40 MFG package unlocks multi-frame generation on RTX 40 cards only.",
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void The_six_builds_are_found_by_their_keys()
    {
        Assert.HasCount(6, OptiScalerBuild.All);
        Assert.AreSame(OptiScalerBuild.Default, OptiScalerBuild.ByKey(""));
        Assert.AreSame(OptiScalerBuild.Wilsjo2Mfg, OptiScalerBuild.ByKey(" WILSJO2-mfg "));
        Assert.IsNull(OptiScalerBuild.ByKey("wilsjo3"));
    }

    [TestMethod]
    public void A_cache_name_is_always_a_file_name()
    {
        string longName = new string('a', 120) + ".zip";
        OptiScalerRelease release = OptiScalerFixtures.Resolved(longName, new NoVerification("test")) with { Tag = "v1/2 beta" };

        Assert.AreEqual("OptiScaler-DLSSNR-v1_2_beta-" + new string('a', 80) + ".zip", release.CacheName);
    }
}
