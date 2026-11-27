using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>Uninstall from the record, the rows of the spec's per-entry table for a file written over the game's own, and for its backup.</summary>
[TestClass]
public sealed class UninstallOriginalTests(TestContext testContext)
{
    private const string Backup = "dxgi.dll" + KnownFiles.BackupSuffix;

    private static readonly string[] _restoredByX =
    [
        "restored: dxgi.dll (the game's own file)", "restored: version.dll (the game's own file)", "restored: d3dcompiler_47.dll (the game's own file)", "removed: dlss5-feed.addon64",
    ];

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task The_games_own_files_come_back_byte_for_byte()
    {
        using TransactionBed bed = new(Switches.GameFiles);
        TreeSnapshot before = bed.Snapshot();
        _ = await bed.CommitAsync(Switches.X(bed), Ct);

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        before.AssertSame(bed.Snapshot());
        UninstallBed.AssertSaid(finished, _restoredByX);
        Assert.AreEqual(4, finished.Removed);
    }

    [TestMethod]
    public async Task The_games_file_takes_its_place_back_over_ours_in_one_step()
    {
        using TransactionBed bed = await UninstallBed.SwappedAsync(Ct);
        var made = CrashPoints.After(int.MaxValue);
        bed.Steps = made;

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.AreEqual("Replace", KindsAt(made, bed.Tree.PathOf("dxgi.dll")));
        Assert.IsTrue(bed.Holds("dxgi.dll", Switches.GameDxgi));
    }

    // The kinds of the steps that changed what is at path, joined by '|'.
    private static string KindsAt(CrashPoints made, string path) =>
        string.Join("|", made.Made.Where(step => step.Path == path || step.Destination == path).Select(step => step.Kind));

    [TestMethod]
    public async Task A_backup_the_store_made_redundant_is_deleted_and_the_file_stays()
    {
        using TransactionBed bed = await UninstallBed.SwappedAsync(Ct);
        bed.Write("dxgi.dll", Switches.GameDxgi);

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(bed.Holds("dxgi.dll", Switches.GameDxgi) && !bed.Exists(Backup));
        Assert.IsTrue(finished.Complete);
    }

    [TestMethod]
    public async Task The_games_newer_file_stays_and_its_old_original_is_set_aside()
    {
        using TransactionBed bed = await UninstallBed.SwappedAsync(Ct);
        bed.Write("dxgi.dll", UninstallBed.Updated);

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(bed.Holds("dxgi.dll", UninstallBed.Updated) && !bed.Exists(Backup));
        Assert.IsTrue(bed.Holds("dxgi.dll" + KnownFiles.SupersededSuffix, Switches.GameDxgi));
        UninstallBed.AssertSaidStarting(finished, "The game had updated dxgi.dll since it was backed up: the older original is kept beside it as dxgi.dll.invidia-claritatis-superseded");
    }

    [TestMethod]
    public async Task A_quarantined_file_over_the_games_has_the_original_put_back()
    {
        using TransactionBed bed = await UninstallBed.SwappedAsync(Ct);
        bed.Delete("dxgi.dll");

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(!bed.Exists(Backup) && bed.Holds("dxgi.dll", Switches.GameDxgi));
        UninstallBed.AssertSaid(finished, "restored: dxgi.dll (the game's own file)");
    }

    [TestMethod]
    public async Task A_damaged_backup_is_not_restored_and_stays_on_the_record()
    {
        using TransactionBed bed = await UninstallBed.SwappedAsync(Ct);
        bed.Write(Backup, Switches.GameVersion);

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(bed.Holds("dxgi.dll", UninstallBed.Ours) && bed.Holds(Backup, Switches.GameVersion));
        Assert.AreEqual("dxgi.dll RestoreFailed", await bed.EntriesAsync(Ct));
        Assert.IsTrue(finished is { Left: 1, Message: "Removed 0 items; 1 could not be removed. Close the game and its launcher, then uninstall again." });
    }

    [TestMethod]
    public async Task A_missing_backup_takes_our_file_out_and_says_the_original_is_gone()
    {
        using TransactionBed bed = await UninstallBed.SwappedAsync(Ct);
        bed.Delete(Backup);

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsFalse(bed.Exists("dxgi.dll"));
        UninstallBed.AssertSaidStarting(finished, "removed: dxgi.dll. The game's own dxgi.dll is gone");
        Assert.IsTrue(finished.Complete);
    }
}
