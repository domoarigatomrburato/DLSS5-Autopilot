using System.Text.Json.Nodes;
using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components.Sources;

/// <summary>DXVK, REFramework, GITC-Uplift, and the pinned RTX40MFG-Unlock and Ultimate ASI Loader.</summary>
[TestClass]
public sealed class ToolReleaseTests(TestContext testContext)
{
    private const string Dxvk = "doitsujin/dxvk";
    private const string Uplift = "ghostinthecamera/GITC-Uplift";
    private const string Mfg = "dashdogy/MFG-Unlocker";
    private const string Loader = "ThirteenAG/Ultimate-ASI-Loader";

    [TestMethod]
    public async Task Dxvk_is_the_windows_archive_never_its_native_sibling()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(ComponentsBed.Api(Dxvk, "/latest"), "dxvk-v3.1.1.json");

        ComponentRelease release = await bed.Resolve(ComponentIds.Dxvk);

        Assert.AreEqual("v3.1.1", release.Version.Label);
        Assert.AreEqual("dxvk-3.1.1.tar.gz", release.Downloads.Single().FileName);
        Assert.IsTrue(release.Downloads[0].IsArchive);
        Assert.AreSequenceEqual(
            ["x64/dxgi.dll", "x64/d3d11.dll", "x64/d3d9.dll", "x64/d3d8.dll", "x32/dxgi.dll", "x32/d3d11.dll", "x32/d3d9.dll", "x32/d3d8.dll"],
            [.. release.Files.Select(file => file.Path)]);
    }

    [TestMethod]
    public async Task A_dxvk_release_with_only_the_native_archive_is_a_changed_layout()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Json(
            ComponentsBed.Api(Dxvk, "/latest"),
            ReleaseJsonText.Release(Dxvk, "v4.0", 0, "dxvk-native-4.0-steamrt-sniper.tar.gz", "dxvk-4.0.zip"));

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Resolve(ComponentIds.Dxvk));

        Assert.AreEqual(ComponentFailure.LayoutChanged, failure.Reason);
    }

    [TestMethod]
    public async Task Reframework_is_the_nightlys_zip_holding_dinput8()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(ComponentsBed.Api("praydog/REFramework-nightly", "/latest"), "reframework-latest.json");

        ComponentRelease release = await bed.Resolve(ComponentIds.Reframework);

        Assert.AreEqual("REFramework.zip", release.Downloads.Single().FileName);
        Assert.AreEqual("dinput8.dll", release.Files.Single().Path);
        Assert.IsInstanceOfType<Sha256Verification>(release.Downloads[0].Verification);
    }

    [TestMethod]
    public async Task Uplift_passes_over_drafts_pre_releases_and_a_release_without_its_zip()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(ComponentsBed.ApiList(Uplift, 10), "gitc-uplift-releases.json");

        ComponentRelease release = await bed.Resolve(ComponentIds.Uplift);

        Assert.AreEqual("v2.1.0", release.Version.Label);
        Assert.AreEqual("GITC-Uplift-2.1.0.zip", release.Downloads.Single().FileName);
        Assert.AreEqual(
            new SumsFileVerification(new(ComponentsBed.Download(Uplift, "v2.1.0", "SHA256SUMS.txt")), "GITC-Uplift-2.1.0.zip"),
            release.Downloads[0].Verification,
            "the sums file wins over the digest: the publisher's own list");
        Assert.AreSequenceEqual(
            ["gitc-uplift.addon64", "gitc-uplift.addon32", "gitc-uplift-helper64.exe", "Uplift.fx", "UpliftMask.fx"],
            [.. release.Files.Select(file => file.Path)]);
    }

    [TestMethod]
    public async Task An_uplift_release_whose_newest_zip_has_no_sums_stops_the_install()
    {
        JsonArray list = ReleaseJsonText.Captured("gitc-uplift-releases.json");
        list.Insert(0, ReleaseJsonText.Release(Uplift, "v2.4.0", -10, "GITC-Uplift-v2.4.0.zip"));
        using ComponentsBed bed = new ComponentsBed(testContext).Json(ComponentsBed.ApiList(Uplift, 10), list);

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Resolve(ComponentIds.Uplift));

        Assert.AreEqual(ComponentFailure.SumsMissing, failure.Reason);
        Assert.AreEqual(
            "ghostinthecamera/GITC-Uplift release v2.4.0 carries no SHA256SUMS.txt. The publisher puts one in every release, so nothing is installed unchecked. Try again later.",
            failure.Message);
    }

    [TestMethod]
    public async Task Uplift_with_no_release_carrying_its_zip_is_a_changed_layout()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Json(
            ComponentsBed.ApiList(Uplift, 10),
            ReleaseJsonText.List(ReleaseJsonText.Release(Uplift, "v3.0.0", 0, "GITC-Uplift-Installer.exe")));

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Resolve(ComponentIds.Uplift));

        Assert.AreEqual(ComponentFailure.LayoutChanged, failure.Reason);
        StringAssert.Contains(failure.Message, "GITC-Uplift carries GITC-Uplift-<version>.zip (newest: v3.0.0)", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task An_empty_uplift_list_names_no_newest_release()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Json(ComponentsBed.ApiList(Uplift, 10), ReleaseJsonText.List());

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Resolve(ComponentIds.Uplift));

        StringAssert.Contains(failure.Message, "(newest: none)", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task The_mfg_unlock_is_its_pinned_tag()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(ComponentsBed.Api(Mfg, "/tags/v1.2.1"), "mfg-unlock-v1.2.1.json");

        ComponentRelease release = await bed.Resolve(ComponentIds.Mfg);

        Assert.AreEqual("v1.2.1", release.Version.Label);
        Assert.AreEqual("Universal-RTX-40-MFG-Unlock-v1.2.1.zip", release.Downloads.Single().FileName);
        Assert.AreSequenceEqual(["RTX40MFGCore.dll", "RTX40MFG.asi", "RTX40MFG-UI.addon64"], [.. release.Files.Select(file => file.Path)]);
        Assert.AreEqual(0, bed.Server.RequestsTo(ComponentsBed.ApiList(Mfg, 20)), "the pin needs no list");
    }

    [TestMethod]
    public async Task Without_the_pin_the_newest_release_of_the_same_shape_is_taken_and_another_shape_passed_over()
    {
        using ComponentsBed bed = new ComponentsBed(testContext)
            .Json(ComponentsBed.Api(Mfg, "/tags/v1.2.1"), ReleaseJsonText.Release(Mfg, "v1.2.1", 5, "README.md"))
            .Json(
                ComponentsBed.ApiList(Mfg, 20),
                ReleaseJsonText.List(
                    ReleaseJsonText.Release(Mfg, "v2.0.0", 0, "MFG-Unlocker-v2.0.0-x64.zip"),
                    ReleaseJsonText.Release(Mfg, "v1.3.0-beta", prerelease: true, 1, "Universal-RTX-40-MFG-Unlock-v1.3.0-beta.zip"),
                    ReleaseJsonText.Release(Mfg, "v1.2.2", 2, "Universal-RTX-40-MFG-Unlock-v1.2.2.zip")));

        ComponentRelease release = await bed.Resolve(ComponentIds.Mfg);

        Assert.AreEqual("v1.2.2", release.Version.Label);
    }

    [TestMethod]
    public async Task With_no_release_of_the_known_shape_the_mfg_unlock_is_a_changed_layout()
    {
        using ComponentsBed bed = new ComponentsBed(testContext)
            .Fixture(ComponentsBed.Api(Mfg, "/tags/v1.2.1"), "rate-limited-403.json", System.Net.HttpStatusCode.NotFound)
            .Json(ComponentsBed.ApiList(Mfg, 20), ReleaseJsonText.List(ReleaseJsonText.Release(Mfg, "v2.0.0", 0, "MFG-Unlocker-v2.0.0-x64.zip")));

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Resolve(ComponentIds.Mfg));

        Assert.AreEqual(ComponentFailure.LayoutChanged, failure.Reason);
        Assert.AreEqual(
            "No recent release of dashdogy/MFG-Unlocker carries Universal-RTX-40-MFG-Unlock-<version>.zip (newest: v2.0.0): the project changed its layout, and a newer layout is not taken on trust. Nothing was placed.",
            failure.Message);
    }

    [TestMethod]
    public async Task The_asi_loader_is_the_64_bit_zip_of_its_pinned_tag()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(ComponentsBed.Api(Loader, "/tags/v9.7.4"), "asi-loader-v9.7.4.json");

        ComponentRelease release = await bed.Resolve(ComponentIds.AsiLoader);

        Assert.AreEqual("v9.7.4", release.Version.Label);
        Assert.AreEqual("Ultimate-ASI-Loader_x64.zip", release.Downloads.Single().FileName);
        Assert.AreEqual("dinput8.dll", release.Files.Single().Path);
    }

    [TestMethod]
    public async Task A_pinned_loader_tag_that_is_gone_says_so()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(ComponentsBed.Api(Loader, "/tags/v9.7.4"), "rate-limited-403.json", System.Net.HttpStatusCode.NotFound);

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Resolve(ComponentIds.AsiLoader));

        Assert.AreEqual(ComponentFailure.NoRelease, failure.Reason);
        Assert.AreEqual("ThirteenAG/Ultimate-ASI-Loader has no release v9.7.4 on GitHub, the release this tool places. Try again later.", failure.Message);
    }

    [TestMethod]
    public async Task A_pinned_loader_release_without_its_zip_is_a_changed_layout()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Json(ComponentsBed.Api(Loader, "/tags/v9.7.4"), ReleaseJsonText.Release(Loader, "v9.7.4", 0, "Ultimate-ASI-Loader.zip"));

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Resolve(ComponentIds.AsiLoader));

        Assert.AreEqual(ComponentFailure.LayoutChanged, failure.Reason);
    }
}
