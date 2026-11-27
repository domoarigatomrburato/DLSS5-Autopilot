using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Net;
using static Autopilot.Core.Tests.Components.ComponentsBed;

namespace Autopilot.Core.Tests.Components.Sources;

/// <summary>LumeniteFX, VORT and ReShade's shader headers: a branch resolved to its head commit, downloaded at that commit, the commit recorded.</summary>
[TestClass]
public sealed class BranchSnapshotTests(TestContext testContext)
{
    [TestMethod]
    public async Task Lumenite_is_the_branch_heads_commit_archive()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(LumeniteHead, "lumenite-mainline-head.json");

        ComponentRelease release = await bed.Sources.ResolveLumeniteAsync(bed.Cancellation);

        Assert.AreEqual(Commit, release.Version.Label);
        ComponentDownload archive = release.Downloads.Single();
        Assert.AreEqual(new Uri($"https://codeload.github.com/umar-afzaal/LumeniteFX/zip/{Commit}"), archive.Url);
        Assert.AreEqual($"LumeniteFX-{Commit}.zip", archive.FileName, "cached by commit, never by the branch's name");
        Assert.AreEqual(ReleaseOrigin.Api, release.Source!.Origin);
    }

    [TestMethod]
    public async Task Lumenite_brings_its_kernel_its_includes_and_its_texture_checked_by_magic_bytes()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(LumeniteHead, "lumenite-mainline-head.json");

        ComponentRelease release = await bed.Sources.ResolveLumeniteAsync(bed.Cancellation);

        Assert.AreEqual(
            new NoVerification("umar-afzaal/LumeniteFX: a commit's archive has no published digest; the commit is the version recorded"),
            release.Downloads[0].Verification);
        Assert.AreSequenceEqual(
            ["Shaders/lumenite_Kernel.fx", "lumenite_Projections.fxh", "lumenite_Helpers.fxh", "lumenite_Compute.fxh", "lumenite_ColorManagement.fxh", "Textures/lumenite_bluenoise256.png"],
            [.. release.Files.Select(file => file.Path)]);
    }

    [TestMethod]
    public async Task Lumenite_is_resolved_once_per_run()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(LumeniteHead, "lumenite-mainline-head.json");

        ComponentRelease kernel = await bed.Sources.ResolveLumeniteAsync(bed.Cancellation);
        ComponentRelease again = await bed.Sources.ResolveLumeniteAsync(bed.Cancellation);

        Assert.AreSame(kernel, again);
        Assert.AreEqual(1, bed.Server.RequestsTo(LumeniteHead), "the API answer is kept like any other");
    }

    [TestMethod]
    public async Task Vort_is_its_main_branch_at_a_commit()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(VortHead, "lumenite-mainline-head.json");

        ComponentRelease release = await bed.Resolve(ComponentIds.Vort);

        Assert.AreEqual($"https://codeload.github.com/vortigern11/vort_Shaders/zip/{Commit}", release.Downloads.Single().Url.AbsoluteUri);
        Assert.AreSequenceEqual(["Shaders/vort_Motion.fx", "Textures/vort_BlueNoise.png"], [.. release.Files.Select(file => file.Path)]);
    }

    [TestMethod]
    public async Task The_shader_headers_are_raw_files_of_one_commit()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(HeadersHead, "lumenite-mainline-head.json");

        ComponentRelease release = await bed.Resolve(ComponentIds.ShaderHeaders);

        Assert.AreEqual(Commit, release.Version.Label);
        Assert.AreSequenceEqual(
            [
                $"https://raw.githubusercontent.com/crosire/reshade-shaders/{Commit}/Shaders/ReShade.fxh",
                $"https://raw.githubusercontent.com/crosire/reshade-shaders/{Commit}/Shaders/ReShadeUI.fxh",
                $"https://raw.githubusercontent.com/crosire/reshade-shaders/{Commit}/Shaders/DrawText.fxh",
            ],
            [.. release.Downloads.Select(download => download.Url.AbsoluteUri)]);
        Assert.IsFalse(release.Downloads[0].IsArchive);
    }
}
