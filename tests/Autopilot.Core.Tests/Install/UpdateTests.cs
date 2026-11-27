using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>Installing again over an install: what it keeps carries over, and a failure returns to the previous working install.</summary>
[TestClass]
public sealed class UpdateTests(TestContext testContext)
{
    private static readonly byte[] _dxgi = GameFolders.Dll("ours");
    private static readonly byte[] _newer = GameFolders.Dll("ours, newer");
    private static readonly byte[] _dlss = GameFolders.Dll("our dlss");

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_failed_update_returns_to_the_previous_working_install()
    {
        using TransactionBed bed = await InstalledAsync(Ct);
        InstallRecord first = await bed.RecordAsync(Ct);
        TreeSnapshot installed = bed.Snapshot();

        InstallFailed failed = await bed.FailAtCommitAsync(Update(bed), Ct);

        installed.AssertSame(bed.Snapshot(), KnownFiles.InstallRecord);
        FullRecord.AssertSame(first, await bed.RecordAsync(Ct));
        Assert.AreEqual(FailureOutcome.RolledBack, failed.Note.Outcome);
    }

    [TestMethod]
    public async Task An_update_carries_over_what_it_keeps_and_never_backs_up_our_file_as_the_games()
    {
        using TransactionBed bed = await InstalledAsync(Ct);
        InstallRecord first = await bed.RecordAsync(Ct);

        _ = await bed.CommitAsync(Update(bed), Ct);

        InstallRecord updated = await bed.RecordAsync(Ct);
        Assert.HasCount(2, updated.Entries);
        Assert.AreEqual(first.Entries[1], updated.Entries[0], "nvngx_dlss.dll is kept as it was, its backup with it");
        Assert.AreEqual(first.Entries[0] with { Written = Plans.Facts(_newer) }, updated.Entries[1], "dxgi.dll's original is the game's, still");
        Assert.IsTrue(bed.BackupIsTheOriginal());
        Assert.AreEqual("", bed.HeldFiles(), "our older dxgi.dll is deleted at commit");
    }

    [TestMethod]
    public async Task A_kept_entry_must_be_on_the_previous_record()
    {
        using TransactionBed bed = new(GameFolders.Empty);

        InstallRefused refused = await bed.InstallAsAsync<InstallRefused>(bed.Plan(RouteId.Native, new KeepEntry("dxgi.dll")), Ct);

        Assert.AreEqual(InstallRefusal.PlanConflict, refused.Reason);
    }

    private static async Task<TransactionBed> InstalledAsync(CancellationToken cancellationToken)
    {
        TransactionBed bed = new(Switches.GameFiles);
        _ = await bed.CommitAsync(bed.Plan(RouteId.Native, Plans.Place("dxgi.dll", _dxgi), Plans.Place("nvngx_dlss.dll", _dlss)), cancellationToken);
        return bed;
    }

    private static InstallPlan Update(TransactionBed bed) => bed.Plan(RouteId.Native, Plans.Place("dxgi.dll", _newer), new KeepEntry("nvngx_dlss.dll"));
}
