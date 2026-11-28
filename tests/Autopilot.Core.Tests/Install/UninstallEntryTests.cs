using Autopilot.Core.Install;
using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>Uninstall from the record, the rows of the spec's per-entry table for a file written over nothing of the game's, and for a config file.</summary>
[TestClass]
public sealed class UninstallEntryTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task Our_file_as_written_is_deleted_and_the_record_with_it()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        TreeSnapshot before = bed.Snapshot();
        await bed.InstallOursAsync(Ct, "a.dll");

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        before.AssertSame(bed.Snapshot());
        Assert.IsTrue(finished is { Complete: true, Removed: 1, Lines: ["removed: a.dll"], Message: "Removed 1 items." });
    }

    [TestMethod]
    public async Task Our_file_changed_since_install_is_left_and_said()
    {
        using TransactionBed bed = await UninstallBed.OursAsync(Ct, "a.dll");
        bed.Write("a.dll", UninstallBed.Updated);

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(bed.Holds("a.dll", UninstallBed.Updated));
        Assert.IsTrue(finished is { Complete: true, Removed: 0, Lines: ["changed since install: not removed: a.dll"] });
    }

    [TestMethod]
    public async Task Our_file_gone_since_install_clears_its_entry()
    {
        using TransactionBed bed = await UninstallBed.OursAsync(Ct, "a.dll");
        bed.Delete("a.dll");

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(finished is { Complete: true, Lines.Count: 0 });
        Assert.IsNull(await bed.FindAsync(Ct));
    }

    [TestMethod]
    public async Task A_config_file_of_ours_is_deleted_whatever_it_holds_now()
    {
        using TransactionBed bed = await UninstallBed.InstalledAsync(GameFolders.Empty, Ct, PlanBeds.Place("ReShade.ini", UninstallBed.OurIni, EntryClass.Config));
        bed.Append("ReShade.ini");

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsFalse(bed.Exists("ReShade.ini"));
        UninstallBed.AssertSaid(finished, "removed: ReShade.ini");
    }

    [TestMethod]
    public async Task A_config_file_over_the_persons_own_brings_theirs_back_whatever_ours_holds_now()
    {
        using TransactionBed bed = await UninstallBed.InstalledAsync(GameFolders.OwnFiles, Ct, PlanBeds.Place("ReShade.ini", UninstallBed.OurIni, EntryClass.Config));
        bed.Append("ReShade.ini");

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.StartsWith("[GENERAL]\r\nEffectSearchPaths=", bed.Read("ReShade.ini"), StringComparison.Ordinal);
        UninstallBed.AssertSaid(finished, "restored: ReShade.ini (the game's own file)");
    }

    [TestMethod]
    public async Task A_path_that_changed_nothing_on_disk_is_not_saved_for()
    {
        using TransactionBed bed = await UninstallBed.OursAsync(Ct, "a.dll", "b.dll");
        bed.Write("a.dll", UninstallBed.Updated);
        var made = CrashPoints.After(int.MaxValue);
        bed.Steps = made;

        _ = await bed.UninstallFinishedAsync(Ct);

        // Removing first, then after b.dll; a.dll, changed since, is left as it is and not saved for.
        Assert.AreEqual(2, made.Made.Count(step => step.Kind == WriteKind.SaveRecord));
    }
}
