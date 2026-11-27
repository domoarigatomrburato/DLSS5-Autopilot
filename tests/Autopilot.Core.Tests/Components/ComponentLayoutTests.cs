using System.Text;
using Autopilot.Core.Components;
using Autopilot.Core.Tests.Archives;

namespace Autopilot.Core.Tests.Components;

/// <summary>Where a fetched component's files are found: by the end of their path, nearest the root, loose or in an archive.</summary>
[TestClass]
public sealed class ComponentLayoutTests(TestContext testContext)
{
    private static readonly string[] _folders = ["x64", "x32"];
    private static readonly string[] _dxvkDlls = ["dxgi.dll", "d3d11.dll", "d3d9.dll", "d3d8.dll", "d3d10core.dll"];

    [TestMethod]
    public async Task An_mfg_zip_of_another_shape_is_refused_naming_what_is_missing()
    {
        using ComponentsBed bed = new(testContext);
        ComponentRelease release = FetchFixtures.Served(bed, ComponentIds.Mfg, "Universal-RTX-40-MFG-Unlock-v1.2.1.zip", MfgWithoutAsi(), "RTX40MFGCore.dll", "RTX40MFG.asi", "RTX40MFG-UI.addon64");

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Files.FetchAsync(release, null, bed.Cancellation));

        Assert.AreEqual(ComponentFailure.LayoutChanged, failure.Reason);
        Assert.AreEqual(
            "dashdogy/MFG-Unlocker v1.0.0 does not carry RTX40MFG.asi: the release changed its layout, and a newer layout is not taken on trust. Nothing was placed.",
            failure.Message);
    }

    [TestMethod]
    public async Task Dxvk_files_are_found_by_their_folder_in_the_tar_gz()
    {
        using ComponentsBed bed = new(testContext);
        ComponentRelease release = FetchFixtures.Served(bed, ComponentIds.Dxvk, "dxvk-3.2.tar.gz", DxvkArchive(), "x64/d3d11.dll", "x32/d3d11.dll");

        FetchedComponent fetched = await bed.Files.FetchAsync(release, null, bed.Cancellation);

        StringAssert.EndsWith(FetchFixtures.PathOf(fetched, "x64/d3d11.dll"), "dxvk-3.2/x64/d3d11.dll", StringComparison.Ordinal);
        StringAssert.EndsWith(FetchFixtures.PathOf(fetched, "x32/d3d11.dll"), "dxvk-3.2/x32/d3d11.dll", StringComparison.Ordinal);
        Assert.HasCount(1, fetched.Folders);
    }

    [TestMethod]
    public async Task The_file_nearest_the_root_is_the_one_taken_and_names_match_in_any_case()
    {
        using ComponentsBed bed = new(testContext);
        byte[] zip = FetchFixtures.Archive(ArchiveFormat.Zip, TestMember.Text("extras/old/dinput8.dll", "old"), TestMember.Text("DINPUT8.DLL", "new"));
        ComponentRelease release = FetchFixtures.Served(bed, ComponentIds.Reframework, "REFramework.zip", zip, "dinput8.dll");

        FetchedComponent fetched = await bed.Files.FetchAsync(release, null, bed.Cancellation);

        StringAssert.EndsWith(FetchFixtures.PathOf(fetched, "dinput8.dll"), "/DINPUT8.DLL", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Loose_files_are_taken_as_they_are_and_an_optional_one_may_be_absent()
    {
        using ComponentsBed bed = new(testContext);
        ComponentRelease release = LooseStandalone(bed);

        FetchedComponent fetched = await bed.Files.FetchAsync(release, null, bed.Cancellation);

        Assert.HasCount(2, fetched.Files);
        Assert.IsFalse(fetched.Files.ContainsKey("StandaloneBoundary.fx"));
        Assert.IsEmpty(fetched.Folders);
    }

    private static byte[] MfgWithoutAsi() =>
        FetchFixtures.Archive(ArchiveFormat.Zip, new("RTX40MFGCore.dll", FetchFixtures.Addon64), new("RTX40MFG-UI.addon64", FetchFixtures.Addon64), FetchFixtures.Text("README.md"));

    private static byte[] DxvkArchive() =>
        FetchFixtures.Archive(
            ArchiveFormat.TarGz,
            [.. _folders.SelectMany(folder => _dxvkDlls.Select(name => new TestMember($"dxvk-3.2/{folder}/{name}", FetchFixtures.Addon64)))]);

    // The add-on and its shader published loose, without the boundary shader later releases carry.
    private static ComponentRelease LooseStandalone(ComponentsBed bed)
    {
        byte[] shader = Encoding.UTF8.GetBytes("technique DLSS5_AIO_Feed {}");
        string addonUrl = ComponentsBed.Download("kibblerz/DLSS5-Reshade-AIO", "v2.0.5", "standalone-dlssnr.addon64");
        string shaderUrl = ComponentsBed.Download("kibblerz/DLSS5-Reshade-AIO", "v2.0.5", "DLSS5_AIO_Feed.fx");
        _ = bed.Server.AnswerBytes(addonUrl, FetchFixtures.Addon64).AnswerBytes(shaderUrl, shader);
        return FetchFixtures.Release(
            ComponentIds.Standalone,
            [FetchFixtures.Download("standalone-dlssnr.addon64", addonUrl, FetchFixtures.Addon64), FetchFixtures.Download("DLSS5_AIO_Feed.fx", shaderUrl, shader)],
            [new("standalone-dlssnr.addon64"), new("DLSS5_AIO_Feed.fx"), new("StandaloneBoundary.fx", Optional: true)]);
    }
}
