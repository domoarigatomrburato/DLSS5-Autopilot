using Autopilot.Core.Components;
using Autopilot.Core.Install.Plans;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Tests.Install.Plans;

[TestClass]
public sealed class PlanGatherTests(TestContext testContext)
{
    [TestMethod]
    public async Task A_gather_takes_ReShades_setup_the_persons_own_add_on_the_cards_build_and_the_runtimes()
    {
        using GatherBed bed = new GatherBed(testContext).UsualMirror();

        GatheredFiles files = await bed.GatherAsync();

        Assert.AreEqual("dlss=310.9.16, dlssd=310.9.16, dlssnr=310.9.16-nvidia, renodx=own file, reshade=6.8.0", Versions(files));
        CollectionAssert.AreEqual(GatherFiles.Dlssnr(120), await File.ReadAllBytesAsync(GatherBedRuns.PathOf(files, "dlssnr", "nvngx_dlssnr.dll"), testContext.CancellationToken));
        CollectionAssert.AreEqual("{}"u8.ToArray(), files.Content("reshade", "ReShade64.json").ToArray());
        Assert.AreEqual(bed.OwnAddon, GatherBedRuns.PathOf(files, "renodx", "renodx-dlss5.addon64"));
        Assert.IsNull(files.Failure("dlssd"));
    }

    [TestMethod]
    public async Task The_persons_label_picks_the_build_and_a_label_no_longer_listed_takes_the_newest_with_a_note()
    {
        using GatherBed chosen = new GatherBed(testContext).UsualMirror();
        using GatherBed missing = new GatherBed(testContext).UsualMirror();

        GatheredFiles older = await chosen.GatherAsync(settings => settings with { DlssnrLabel = "310.8.0" });
        GatheredFiles newest = await missing.GatherAsync(settings => settings with { DlssnrLabel = "310.7.1" });

        Assert.AreEqual("310.8.0", older.Versions["dlssnr"]);
        Assert.AreEqual("310.9.16-nvidia", newest.Versions["dlssnr"]);
        CollectionAssert.Contains(newest.Notes.ToList(), "nvngx_dlssnr.dll 310.7.1 is not on the build list any more; 310.9.16-nvidia, the newest, was taken instead");
    }

    [TestMethod]
    public async Task No_build_for_the_card_stops_the_gather()
    {
        using GatherBed bed = new GatherBed(testContext).Mirror(
            ("dlssnr-310.9.16-nvidia", "nvngx_dlssnr.dll", GatherFiles.Dlssnr(75)),
            ("dlssnr-310.8.0", "nvngx_dlssnr.dll", GatherFiles.Dlssnr(75)),
            ("dlss-310.9.16", "nvngx_dlss.dll", GatherFiles.Dll("mirror dlss")));

        ComponentException refused = await Assert.ThrowsExactlyAsync<ComponentException>(() => bed.GatherAsync());

        Assert.AreEqual(ComponentFailure.NoUsableBuild, refused.Reason);
    }

    [TestMethod]
    public async Task A_required_runtime_missing_from_its_download_stops_the_gather_before_anything_is_placed()
    {
        using GatherBed bed = new GatherBed(testContext).Mirror(
            ("dlssnr-310.9.16-nvidia", "nvngx_dlssnr.dll", GatherFiles.Dlssnr(120)),
            ("dlss-310.9.16", "readme.txt", "not a runtime"u8.ToArray()));

        ComponentException refused = await Assert.ThrowsExactlyAsync<ComponentException>(() => bed.GatherAsync());

        Assert.AreEqual(ComponentFailure.NoUsableBuild, refused.Reason);
        StringAssert.StartsWith(refused.Message, "nvngx_dlss.dll", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task A_ray_reconstruction_runtime_that_cannot_be_fetched_is_a_skipped_line_and_a_warning()
    {
        using GatherBed bed = new GatherBed(testContext).Mirror(
            ("dlssnr-310.9.16-nvidia", "nvngx_dlssnr.dll", GatherFiles.Dlssnr(120)),
            ("dlss-310.9.16", "nvngx_dlss.dll", GatherFiles.Dll("mirror dlss")),
            ("dlssd-310.9.16", "readme.txt", "not a runtime"u8.ToArray()));

        GatheredFiles files = await bed.GatherAsync();

        StringAssert.Contains(files.Failure("dlssd"), "310.9.16", StringComparison.Ordinal);
        Assert.AreEqual("nvngx_dlssd.dll: the download did not arrive", string.Join(" | ", files.Skipped));
    }

    [TestMethod]
    public async Task NVIDIAs_own_runtimes_come_first_unless_the_person_named_a_build()
    {
        using GatherBed bed = new GatherBed(testContext).UsualMirror().Nvidia(GatherFiles.Dll("NVIDIA's dlss"));

        GatheredFiles files = await bed.GatherAsync();

        Assert.AreEqual("310.9.16 (NVIDIA SDK)", files.Versions["dlss"]);
        Assert.AreEqual("310.9.16", files.Versions["dlssd"]);
    }

    [TestMethod]
    public async Task A_file_from_NVIDIA_that_is_not_a_runtime_is_deleted_from_the_cache_and_the_mirrors_copy_taken()
    {
        using GatherBed bed = new GatherBed(testContext).UsualMirror().Nvidia("MZ but nothing else"u8.ToArray());

        GatheredFiles files = await bed.GatherAsync();

        Assert.AreEqual("310.9.16", files.Versions["dlss"]);
        Assert.AreEqual(1, bed.Components.Server.RequestsTo(GatherFiles.NvidiaRaw("nvngx_dlss.dll")));
        Assert.AreEqual(0, bed.Downloads("nvngx_dlss.dll"));
    }

    [TestMethod]
    public async Task A_route_whose_gather_is_not_written_yet_is_refused_before_anything_is_asked()
    {
        using GatherBed bed = new(testContext);

        NotSupportedException refused = await Assert.ThrowsExactlyAsync<NotSupportedException>(() => bed.GatherAsync(route: RouteId.Upstream));

        Assert.Contains("'upstream'", refused.Message);
        Assert.IsEmpty(bed.Components.Server.Requests);
    }

    private static string Versions(GatheredFiles files) => string.Join(", ", files.Versions.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
}
