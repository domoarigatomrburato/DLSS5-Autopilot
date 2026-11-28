using System.Runtime.CompilerServices;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>The game's own file kept beside ours exactly, and put back exactly when the install rolls back.</summary>
[TestClass]
public sealed class BackupTests(TestContext testContext)
{
    private static readonly byte[] _dxgi = GameFolders.Dll("ours");
    private static readonly byte[] _addon = GameFolders.Dll("our add-on");

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task The_games_own_dxgi_is_backed_up_and_comes_back_on_rollback_with_its_attributes_and_time()
    {
        using TransactionBed bed = new(Switches.GameFiles);
        TreeSnapshot before = bed.Snapshot();
        InstallPlan plan = DxgiAndAddOn(bed);
        StrongBox<bool> backedUp = await bed.LookAtCommitAsync(plan, BackedUp, Ct);

        InstallFailed failed = await bed.FailAtCommitAsync(plan, Ct);

        AssertRestored(before, bed, backedUp.Value, failed);
    }

    [TestMethod]
    public async Task An_installed_dxgi_keeps_the_games_original_on_the_record()
    {
        using TransactionBed bed = new(Switches.GameFiles);

        _ = await bed.CommitAsync(bed.Plan(RouteId.Native, PlanBeds.Place("dxgi.dll", _dxgi)), Ct);

        AssertOriginal(bed, await bed.RecordAsync(Ct));
    }

    private static InstallPlan DxgiAndAddOn(TransactionBed bed) => bed.Plan(RouteId.Native, PlanBeds.Place("dxgi.dll", _dxgi), PlanBeds.Place("dlss5-feed.addon64", _addon));

    private static void AssertRestored(TreeSnapshot before, TransactionBed bed, bool backedUp, InstallFailed failed)
    {
        Assert.IsTrue(backedUp, "while installed, the game's dxgi.dll is its backup, exactly");
        Assert.AreEqual(FailureOutcome.RolledBack, failed.Note.Outcome);
        before.AssertSame(bed.Snapshot());
    }

    private static bool BackedUp(TransactionBed bed) => bed.BackupIsTheOriginal() && bed.Holds("dxgi.dll", _dxgi);

    private static void AssertOriginal(TransactionBed bed, InstallRecord record)
    {
        Assert.IsTrue(bed.BackupIsTheOriginal());
        OriginalFile expected = new(PlanBeds.Facts(Switches.GameDxgi), FileAttributes.ReadOnly, Switches.GameTime.UtcDateTime, BackupKind.Backup);
        Assert.AreEqual(expected, record.Entries.Single().Original);
    }
}
