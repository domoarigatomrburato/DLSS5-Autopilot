using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>What uninstall takes out after the entries: the helper folder, the runtime logs, the folders the install created.</summary>
[TestClass]
public sealed class UninstallStepTests(TestContext testContext)
{
    private const string Helper = "host64/dlss5-feed-host64.exe";

    private static readonly int[] _shortWaits = [100, 100, 100, 100, 100];

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task The_helper_folder_goes_once_its_recorded_files_are_out()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        _ = bed.Tree.Folder("host64");
        await bed.InstallOursAsync(Ct, Helper);

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.IsFalse(bed.HasFolder("host64"));
    }

    [TestMethod]
    public async Task A_helper_folder_holding_other_files_stays_with_them()
    {
        using TransactionBed bed = await UninstallBed.InstalledAsync([TreeFile.Text("host64/notes.txt", "mine")], Ct, PlanBeds.Place(Helper, UninstallBed.Ours));

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(bed.Exists("host64/notes.txt") && !bed.Exists(Helper));
    }

    [TestMethod]
    public async Task Runtime_logs_go_by_exact_name_and_the_games_own_logs_stay()
    {
        using TransactionBed bed = await UninstallBed.InstalledAsync([TreeFile.Text("Logs/game.log", "the game's")], Ct, PlanBeds.Place("a.dll", UninstallBed.Ours));
        bed.Plant("ReShade.log", "nvngx.log", "dlss5-feed.log", "Game_d3d11.log", "other.log", "Logs/OptiScaler.log");

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsFalse(bed.Exists("ReShade.log") || bed.Exists("nvngx.log") || bed.Exists("dlss5-feed.log") || bed.Exists("Logs/OptiScaler.log"));
        Assert.IsTrue(bed.Exists("Game_d3d11.log") && bed.Exists("other.log") && bed.Exists("Logs/game.log"));
        UninstallBed.AssertSaid(finished, "removed: ReShade.log", "removed: Logs/OptiScaler.log");
        Assert.AreEqual(5, finished.Removed);
    }

    [TestMethod]
    public async Task A_log_another_program_holds_stays_and_the_uninstall_still_completes()
    {
        using TransactionBed bed = await UninstallBed.OursAsync(Ct, "a.dll");
        bed.Plant("ReShade.log");
        _ = bed.Hold("ReShade.log");

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(bed.Exists("ReShade.log") && !bed.Exists("a.dll"));
        Assert.IsTrue(finished.Complete);
        CollectionAssert.AreEqual(_shortWaits, bed.WaitsInMilliseconds(), "a log is no entry: tried only for a moment");
    }

    [TestMethod]
    public async Task OptiScalers_log_folder_goes_when_its_files_were_the_only_ones()
    {
        using TransactionBed bed = await UninstallBed.OursAsync(Ct, "a.dll");
        bed.Plant("Logs/OptiScaler.log", "Logs/optiscaler-2.log");

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.IsFalse(bed.HasFolder("Logs"));
    }

    [TestMethod]
    public async Task An_empty_log_folder_the_install_found_stays()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        _ = bed.Tree.Folder("Logs");
        await bed.InstallOursAsync(Ct, "a.dll");

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(bed.HasFolder("Logs"));
    }

    [TestMethod]
    public async Task DXVKs_logs_go_only_when_DXVK_was_ours()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        _ = await bed.CommitAsync(WithDxvk(bed), Ct);
        bed.Plant("Game_d3d11.log", "Game_dxgi.log", "Other_d3d11.log");

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.IsFalse(bed.Exists("Game_d3d11.log") || bed.Exists("Game_dxgi.log"));
        Assert.IsTrue(bed.Exists("Other_d3d11.log"));
    }

    [TestMethod]
    public async Task Folders_the_install_created_go_deepest_first_and_the_persons_stay()
    {
        using TransactionBed bed = await UninstallBed.InstalledAsync(
            [TreeFile.Text("reshade-shaders/Textures/mine.png", "mine")],
            Ct,
            PlanBeds.Place("reshade-shaders/Shaders/include/a.fxh", UninstallBed.Ours),
            PlanBeds.Place("mods/new/b.dll", UninstallBed.Ours));
        _ = bed.Tree.Folder("mods/new/empty-of-mine");

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.IsFalse(bed.HasFolder("reshade-shaders/Shaders"));
        Assert.IsTrue(bed.Exists("reshade-shaders/Textures/mine.png") && bed.HasFolder("mods/new/empty-of-mine"));
    }

    private static InstallPlan WithDxvk(TransactionBed bed)
    {
        InstallPlan plan = bed.OursPlan("d3d11.dll");
        return plan with { Header = plan.Header with { Components = new Dictionary<string, string> { ["dxvk"] = "2.7" } } };
    }
}
