using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>"What will happen?", per install.md's preview: its lines, in order and in their wording.</summary>
[TestClass]
public sealed class PreviewLineTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task The_lines_come_in_order_warnings_clean_up_writes_backups_and_outside()
    {
        using RefusalBed bed = new(PreviewScenes.Folder());

        InstallPreview preview = await bed.PreviewAsync(PreviewScenes.BetaRequest(bed), Ct);

        Assert.AreEqual(
            """
            warning: native is beta on this game: it works in many games, not all. If nothing changes in the game, try another route.
            will clean up first: dlss5-bridge.addon64
            will write 4 file(s) (dxgi.dll, ReShade.ini, renodx-dlss5.addon64, ...)
            will back up: dxgi.dll, dlss5-bridge.addon64
            nothing is written outside this folder
            """,
            string.Join('\n', preview.Lines()));
        Assert.AreEqual("ReShade, Add-on", string.Join(", ", preview.Steps));
    }

    [TestMethod]
    public async Task A_blocker_shows_only_what_stops_the_install()
    {
        using RefusalBed bed = new(PreviewScenes.Folder());
        _ = bed.Processes.Run(bed.Tree.PathOf("Game.exe"));

        InstallPreview preview = await bed.PreviewAsync(PreviewScenes.BetaRequest(bed), Ct);

        Assert.StartsWith("cannot install: Game.exe is running. Close the game first", preview.Lines().Single());
    }

    [TestMethod]
    public async Task Nothing_of_the_games_overwritten_needs_no_backup()
    {
        using RefusalBed bed = new();

        InstallPreview preview = await bed.PreviewAsync(bed.Request(RouteId.Native, placed: "renodx-dlss5.addon64"), Ct);

        Assert.AreEqual(
            """
            will write 1 file(s) (renodx-dlss5.addon64)
            nothing of yours is overwritten - no backups needed
            nothing is written outside this folder
            """,
            string.Join('\n', preview.Lines()));
    }

    [TestMethod]
    public async Task A_file_list_only_a_download_reveals_is_a_pattern_line()
    {
        using RefusalBed bed = new();

        InstallPreview preview = await bed.PreviewAsync(bed.Request(PreviewScenes.PatternPlan(bed)), Ct);

        Assert.AreEqual(
            "will write 2 file(s) (reshade-shaders/Shaders/lumenite_*.fxh (a pattern: the list is known after the download), reshade-shaders/Shaders/DLSS5_Feed.fx)",
            preview.Lines()[0]);
        Assert.IsTrue(preview.Written[0].IsPattern);
    }

    [TestMethod]
    public async Task A_plan_with_a_pattern_is_never_installed_as_it_is()
    {
        using RefusalBed bed = new();

        _ = await Assert.ThrowsExactlyAsync<ArgumentException>(() => bed.Bed.InstallAsync(PreviewScenes.PatternPlan(bed), Ct));
    }

    [TestMethod]
    public void An_effect_outside_the_folder_gives_its_outside_line()
    {
        using GameTree tree = new();

        InstallPreview preview = OutsideEffects.Preview(tree.Root);

        Assert.AreEqual("outside: the Vulkan layer (every Vulkan application of this account, 'uninstall' removes it)", preview.Lines()[^1]);
    }
}
