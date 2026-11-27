using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>Files another program holds: a target fails as locked, and a rollback that cannot finish leaves the record open.</summary>
[TestClass]
public sealed class LockedFileTests(TestContext testContext)
{
    private static readonly byte[] _ours = GameFolders.Dll("ours");
    private static readonly byte[] _addon = GameFolders.Dll("our add-on");

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_target_another_program_holds_fails_as_locked_and_leaves_the_folder_as_it_was()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        TreeSnapshot before = bed.Snapshot();
        bed.Seam = new HeldPaths().Hold(bed.Tree.PathOf("a.dll")).Seam;

        InstallFailed failed = await bed.InstallAsAsync<InstallFailed>(TwoFiles(bed), Ct);

        AssertLocked(before, bed, failed, "a.dll");
    }

    [TestMethod]
    public async Task Our_previous_file_another_program_holds_fails_the_switch_as_locked_and_keeps_the_previous_route()
    {
        using TransactionBed bed = await Switches.InstalledAsync(Ct);
        InstallRecord x = await bed.RecordAsync(Ct);
        TreeSnapshot before = bed.Snapshot();
        bed.Seam = new HeldPaths().Hold(bed.Tree.PathOf("dxgi.dll")).Seam;

        InstallFailed failed = await bed.InstallAsAsync<InstallFailed>(Switches.Y(bed), Ct);

        AssertLocked(before, bed, failed, "dxgi.dll");
        Switches.AssertBackToX(before, bed, x, await bed.RecordAsync(Ct));
    }

    [TestMethod]
    public async Task A_backup_that_cannot_be_made_refuses_the_write_and_overwrites_nothing()
    {
        using TransactionBed bed = new(Switches.GameFiles);
        TreeSnapshot before = bed.Snapshot();
        bed.Seam = new HeldPaths().Hold(bed.Tree.PathOf("dxgi.dll")).Seam;

        InstallFailed failed = await bed.InstallAsAsync<InstallFailed>(AddOnAndDxgi(bed), Ct);

        AssertLocked(before, bed, failed, "dxgi.dll");
        Assert.StartsWith(failed.Opening + "\n\ndxgi.dll could not be backed up (", failed.Note.Text, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task A_rollback_a_held_file_stops_leaves_the_record_open_and_says_so()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        HeldPaths held = new();
        bed.Seam = held.Seam;
        InstallPlan plan = TwoFiles(bed);
        _ = await bed.LookAtCommitAsync(plan, look => held.Hold(look.Tree.PathOf("a.dll")), Ct);

        InstallFailed failed = await bed.FailAtCommitAsync(plan, Ct);

        AssertStuck(bed, failed, await bed.RecordAsync(Ct));
    }

    private static InstallPlan AddOnAndDxgi(TransactionBed bed) => bed.Plan(RouteId.Native, Plans.Place("dlss5-feed.addon64", _addon), Plans.Place("dxgi.dll", _ours));

    private static InstallPlan TwoFiles(TransactionBed bed) => bed.Plan(RouteId.Native, Plans.Place("b.dll", _ours), Plans.Place("a.dll", _ours));

    private static void AssertLocked(TreeSnapshot before, TransactionBed bed, InstallFailed failed, string path)
    {
        before.AssertSame(bed.Snapshot(), KnownFiles.InstallRecord);
        Assert.AreEqual(FailureKind.Locked, failed.Note.Kind);
        Assert.Contains(path, failed.Note.Text);
    }

    private static void AssertStuck(TransactionBed bed, InstallFailed failed, InstallRecord record)
    {
        Assert.AreEqual(FailureOutcome.RollbackIncomplete, failed.Note.Outcome);
        Assert.AreEqual(RecordState.Removing, record.State);
        Assert.AreEqual("a.dll", string.Join("|", record.Entries.Select(entry => entry.Path)));
        Assert.IsTrue(bed.Exists("a.dll") && !bed.Exists("b.dll"));
    }
}
